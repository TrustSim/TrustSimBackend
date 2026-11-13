using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Trade.Commands;

/// <summary>
/// Handler xử lý Admin duyệt hồ sơ bằng chứng sang tên SIM từ Bên Bán hoặc Bên Mua,
/// tự động niêm phong Văn bản số 3 (Biên bản nghiệm thu & thanh lý) và cập nhật quyền sở hữu SIM khi đủ 2 bên.
/// </summary>
public class ReviewSimTransferProofCommandHandler : IRequestHandler<ReviewSimTransferProofCommand, SimTransferDetailsDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;
    private readonly INotificationService _notificationService;
    private readonly IPdfContractService _pdfContractService;
    private readonly IPdfSigningService _pdfSigningService;
    private readonly ICloudStorageService _cloudStorageService;
    private readonly IAuctionDelayQueueService _delayQueue;
    private readonly ILogger<ReviewSimTransferProofCommandHandler> _logger;

    public ReviewSimTransferProofCommandHandler(
        IUnitOfWork unitOfWork,
        ILedgerService ledgerService,
        INotificationService notificationService,
        IPdfContractService pdfContractService,
        IPdfSigningService pdfSigningService,
        ICloudStorageService cloudStorageService,
        IAuctionDelayQueueService delayQueue,
        ILogger<ReviewSimTransferProofCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _ledgerService = ledgerService;
        _notificationService = notificationService;
        _pdfContractService = pdfContractService;
        _pdfSigningService = pdfSigningService;
        _cloudStorageService = cloudStorageService;
        _delayQueue = delayQueue;
        _logger = logger;
    }

    public async Task<SimTransferDetailsDto> Handle(ReviewSimTransferProofCommand request, CancellationToken cancellationToken)
    {
        var trade = await _unitOfWork.TradeTransactions.GetByIdAsync(request.TradeId, cancellationToken);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch chuyển nhượng có ID '{request.TradeId}'.");
        }

        if (trade.SimTransfer == null)
        {
            throw new BadRequestException("Tiến trình bàn giao SIM chưa được khởi tạo cho giao dịch này.");
        }

        if (trade.SimTransfer.SimTransferStatus == SimTransferStatus.COMPLETED)
        {
            throw new BadRequestException("Giao dịch chuyển nhượng SIM này đã hoàn tất thành công trước đó.");
        }

        if (trade.SimTransfer.SimTransferStatus == SimTransferStatus.CANCELLED)
        {
            throw new BadRequestException("Giao dịch chuyển nhượng SIM này đã bị hủy bỏ.");
        }

        var isTargetSeller = string.Equals(request.TargetParty, "SELLER", StringComparison.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;

        if (isTargetSeller)
        {
            // --- DUYỆT BẰNG CHỨNG BÊN BÁN ---
            var sellerProofs = trade.SimTransfer.TransferProofs
                .Where(p => p.ProofType == TransferProofType.SELLER_HANDOVER)
                .ToList();

            if (sellerProofs.Count == 0)
            {
                throw new BadRequestException("Người Bán chưa nộp bất kỳ bằng chứng sang tên nào để duyệt.");
            }

            var targetSellerProofs = sellerProofs.Where(p => p.ProofStatus == ProofStatus.PENDING).ToList();
            if (targetSellerProofs.Count == 0)
            {
                targetSellerProofs = sellerProofs;
            }

            if (request.IsApproved)
            {
                foreach (var proof in targetSellerProofs)
                {
                    proof.ProofStatus = ProofStatus.APPROVED;
                    proof.VerifiedAt = now;
                }

                trade.SimTransfer.SimTransferStatus = SimTransferStatus.PENDING_BUYER_ACTION;

                _logger.LogInformation(
                    "[Admin Review] Admin {AdminId} đã DUYỆT biên bản của Bên Bán cho giao dịch {TradeCode}",
                    request.AdminUserId, trade.TradeTransactionCode);

                await _notificationService.SendNotificationAsync(
                    trade.BuyerId,
                    NotificationType.TRADE_TRANSFERRING_SIM,
                    "Biên bản sang tên SIM đã được duyệt!",
                    $"Hồ sơ sang tên của Bên Bán cho SIM {trade.SimNumber?.PhoneNumber} đã được Ban Quản Trị duyệt hợp lệ. Vui lòng kiểm tra sóng và tải ảnh nghiệm thu lên hệ thống.",
                    $"/trade/{trade.Id}",
                    cancellationToken: cancellationToken);

                await _notificationService.SendNotificationAsync(
                    trade.SellerId,
                    NotificationType.TRADE_TRANSFERRING_SIM,
                    "Biên bản sang tên SIM đã được phê duyệt",
                    $"Biên bản sang tên cho SIM {trade.SimNumber?.PhoneNumber} đã được Ban Quản Trị duyệt thành công. Hệ thống đang yêu cầu Bên Mua nghiệm thu SIM.",
                    $"/trade/{trade.Id}",
                    cancellationToken: cancellationToken);
            }
            else
            {
                foreach (var proof in targetSellerProofs)
                {
                    proof.ProofStatus = ProofStatus.REJECTED;
                    proof.Description = !string.IsNullOrWhiteSpace(request.RejectionReason)
                        ? $"{proof.Description} [Từ chối: {request.RejectionReason}]"
                        : proof.Description;
                    proof.VerifiedAt = now;
                }

                trade.SimTransfer.SimTransferStatus = SimTransferStatus.PENDING_SELLER_ACTION;

                _logger.LogWarning(
                    "[Admin Review] Admin {AdminId} đã TỪ CHỐI biên bản của Bên Bán cho {TradeCode}. Lý do: {Reason}",
                    request.AdminUserId, trade.TradeTransactionCode, request.RejectionReason);

                await _notificationService.SendNotificationAsync(
                    trade.SellerId,
                    NotificationType.TRADE_TRANSFERRING_SIM,
                    "Biên bản sang tên SIM bị từ chối",
                    $"Hồ sơ sang tên cho SIM {trade.SimNumber?.PhoneNumber} bị từ chối với lý do: {request.RejectionReason ?? "Hồ sơ không hợp lệ"}. Vui lòng nộp lại biên bản chính xác.",
                    $"/trade/{trade.Id}",
                    cancellationToken: cancellationToken);
            }
        }
        else
        {
            // --- DUYỆT BẰNG CHỨNG BÊN MUA (BẮT BUỘC BÊN BÁN ĐÃ ĐƯỢC DUYỆT TRƯỚC) ---
            var isSellerAlreadyApproved = trade.SimTransfer.TransferProofs
                .Any(p => p.ProofType == TransferProofType.SELLER_HANDOVER && p.ProofStatus == ProofStatus.APPROVED);

            if (!isSellerAlreadyApproved)
            {
                throw new BadRequestException("Phải phê duyệt bằng chứng chuyển nhượng của Người Bán trước khi duyệt bằng chứng nhận SIM của Người Mua.");
            }

            var buyerProofs = trade.SimTransfer.TransferProofs
                .Where(p => p.ProofType == TransferProofType.BUYER_RECEIPT)
                .ToList();

            if (buyerProofs.Count == 0)
            {
                throw new BadRequestException("Người Mua chưa nộp bất kỳ ảnh nghiệm thu nào để duyệt.");
            }

            var targetBuyerProofs = buyerProofs.Where(p => p.ProofStatus == ProofStatus.PENDING).ToList();
            if (targetBuyerProofs.Count == 0)
            {
                targetBuyerProofs = buyerProofs;
            }

            if (!request.IsApproved)
            {
                foreach (var proof in targetBuyerProofs)
                {
                    proof.ProofStatus = ProofStatus.REJECTED;
                    proof.Description = !string.IsNullOrWhiteSpace(request.RejectionReason)
                        ? $"{proof.Description} [Từ chối: {request.RejectionReason}]"
                        : proof.Description;
                    proof.VerifiedAt = now;
                }

                trade.SimTransfer.SimTransferStatus = SimTransferStatus.PENDING_BUYER_ACTION;

                _logger.LogWarning(
                    "[Admin Review] Admin {AdminId} đã TỪ CHỐI ảnh nghiệm thu của Bên Mua cho {TradeCode}. Lý do: {Reason}",
                    request.AdminUserId, trade.TradeTransactionCode, request.RejectionReason);

                await _notificationService.SendNotificationAsync(
                    trade.BuyerId,
                    NotificationType.TRADE_TRANSFERRING_SIM,
                    "Ảnh nghiệm thu SIM bị từ chối",
                    $"Ảnh nghiệm thu SIM {trade.SimNumber?.PhoneNumber} bị từ chối với lý do: {request.RejectionReason ?? "Ảnh không đạt yêu cầu"}. Vui lòng tải lại ảnh kiểm tra sóng / app nhà mạng.",
                    $"/trade/{trade.Id}",
                    cancellationToken: cancellationToken);
            }
            else
            {
                foreach (var proof in targetBuyerProofs)
                {
                    proof.ProofStatus = ProofStatus.APPROVED;
                    proof.VerifiedAt = now;
                }

                _logger.LogInformation(
                    "[Admin Review] Admin {AdminId} đã DUYỆT ảnh nghiệm thu của Bên Mua cho giao dịch {TradeCode}",
                    request.AdminUserId, trade.TradeTransactionCode);
            }
        }

        // KIỂM TRA ĐIỀU KIỆN ĐÃ DUYỆT CẢ 2 BÊN (BẤT KỂ THỨ TỰ DUYỆT BÊN BÁN HAY BÊN MUA TRƯỚC)
        var isSellerApproved = trade.SimTransfer.TransferProofs
            .Any(p => p.ProofType == TransferProofType.SELLER_HANDOVER && p.ProofStatus == ProofStatus.APPROVED);
        var isBuyerApproved = trade.SimTransfer.TransferProofs
            .Any(p => p.ProofType == TransferProofType.BUYER_RECEIPT && p.ProofStatus == ProofStatus.APPROVED);

        if (request.IsApproved && isSellerApproved && isBuyerApproved)
        {
            _logger.LogInformation(
                "[Admin Review] 🎯 Giao dịch {TradeCode} đã DUYỆT CẢ 2 BÊN! Kích hoạt sinh Văn Bản 3, chuyển sở hữu SIM và giải ngân Escrow...",
                trade.TradeTransactionCode);

            // 1. TẠO VĂN BẢN SỐ 3 (Biên bản nghiệm thu & thanh lý chuyển nhượng SIM)
            if (trade.TransferContract != null && string.IsNullOrWhiteSpace(trade.TransferContract.LiquidationDocumentUrl))
            {
                try
                {
                    var rawLiqBytes = await _pdfContractService.GenerateLiquidationMinutePdfAsync(trade.Id, cancellationToken);
                    var isAuto = trade.TransferContract.IsAutoEnforced;
                    var stampBoxes = await _pdfContractService.GetStampBoxesAsync(trade.Id, isAuto, cancellationToken);

                    var signResult = await _pdfSigningService.SignPdfPadesAsync(
                        rawLiqBytes,
                        "Nghiệm thu hoàn tất chuyển quyền sử dụng SIM và thanh lý giao dịch điện tử",
                        "Hà Nội, Việt Nam",
                        stampBoxes,
                        cancellationToken);

                    var simPhone = trade.SimNumber?.PhoneNumber?.Replace("+", "") ?? "SIM";
                    var liqFileName = $"BienBanThanhLy_{trade.TradeTransactionCode}_{simPhone}_Signed.pdf";
                    var liqUrl = await _cloudStorageService.UploadImageBytesAsync(
                        signResult.SignedPdfBytes,
                        liqFileName,
                        "contracts",
                        "application/pdf",
                        cancellationToken);

                    trade.TransferContract.LiquidationDocumentUrl = liqUrl;
                    trade.TransferContract.LiquidationDocumentHash = signResult.Sha256Hash;
                    trade.TransferContract.LiquidationSealedAt = signResult.SignedAt;
                    trade.TransferContract.ContractStatus = ContractStatus.COMPLETED;
                    trade.TransferContract.BlockchainTxHash = $"0x{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{trade.TradeTransactionCode}_{signResult.Sha256Hash}_{signResult.SignedAt:O}"))).ToLowerInvariant()}";

                    _logger.LogInformation(
                        "[Admin Review] ✅ Đã lưu Biên bản thanh lý cho {TradeCode}: {Url} (Hash: {Hash})",
                        trade.TradeTransactionCode, liqUrl, signResult.Sha256Hash);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Admin Review] ⚠️ Lỗi trong quá trình sinh/ký biên bản thanh lý cho {TradeCode}. Vẫn tiếp tục giải ngân giao dịch.", trade.TradeTransactionCode);
                }
            }

            if (trade.TransferContract != null)
            {
                trade.TransferContract.ContractStatus = ContractStatus.COMPLETED;
            }

            // 2. CẬP NHẬT NHÓM BẢNG QUYỀN SỞ HỮU SIM
            var oldOwnership = await _unitOfWork.SimOwnerships.GetActiveOwnershipBySimNumberIdAsync(trade.SimNumberId, cancellationToken);
            if (oldOwnership != null)
            {
                oldOwnership.OwnershipStatus = OwnershipStatus.ENDED;
                _unitOfWork.SimOwnerships.Update(oldOwnership);
            }

            var newBuyerOwnership = new SimOwnership
            {
                Id = Guid.NewGuid(),
                SimNumberId = trade.SimNumberId,
                UserAccountId = trade.BuyerId,
                OwnershipStatus = OwnershipStatus.ACTIVE,
                AcquistionType = AcquistionType.AUCTION_TRANSFER
            };
            await _unitOfWork.SimOwnerships.AddAsync(newBuyerOwnership, cancellationToken);

            var verification = new SimVerification
            {
                Id = Guid.NewGuid(),
                SimOwnershipId = newBuyerOwnership.Id,
                VerificationStatus = VerificationStatus.VERIFIED,
                VerificationMethod = "AUCTION_TRANSFER_APPROVED",
                RequestedAt = now,
                VerifiedAt = now
            };
            await _unitOfWork.SimVerifications.AddAsync(verification, cancellationToken);

            if (trade.SimNumber != null)
            {
                trade.SimNumber.SimStatus = SimStatus.TRANSFERRED;
                _unitOfWork.SimNumbers.Update(trade.SimNumber);
            }

            // 3. GIẢI NGÂN QUỸ ESCROW
            var escrowAmount = trade.Escrow?.EscrowAmount ?? 0m;
            var platformFeePercent = 0.05m;
            var platformFeeAmount = Math.Round(escrowAmount * platformFeePercent, 2);
            var netSellerPayout = escrowAmount - platformFeeAmount;

            if (trade.Escrow != null && trade.Escrow.EscrowStatus == EscrowStatus.HELD && escrowAmount > 0)
            {
                var sellerWallet = await _unitOfWork.Wallets.GetByUserIdAsync(trade.SellerId, cancellationToken);
                if (sellerWallet == null)
                {
                    throw new NotFoundException($"Không tìm thấy ví tiền của người bán ID '{trade.SellerId}'.");
                }

                if (sellerWallet.WalletStatus == WalletStatus.LOCKED)
                {
                    throw new BadRequestException("Ví tiền người bán đang bị khóa kỷ luật, không thể nhận tiền giải ngân.");
                }

                var journal = await _ledgerService.PostEscrowReleaseAsync(
                    sellerWallet.Id,
                    escrowAmount,
                    platformFeeAmount,
                    trade.Escrow.Id,
                    cancellationToken);

                trade.Escrow.EscrowStatus = EscrowStatus.RELEASED;
                trade.Escrow.ReleasedAt = now;

                var payoutTx = new AuctionTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = sellerWallet.Id,
                    AuctionId = trade.AuctionId,
                    EscrowId = trade.Escrow.Id,
                    AuctionTransactionType = AuctionTransactionType.SELLER_PAYOUT,
                    TransactionStatus = TransactionStatus.COMPLETED,
                    Amount = netSellerPayout,
                    BalanceBefore = sellerWallet.AvailableBalance - netSellerPayout,
                    BalanceAfter = sellerWallet.AvailableBalance,
                    LedgerJournalId = journal.Id,
                    JournalCode = journal.JournalCode,
                    CreatedAt = now,
                    CompletedAt = now
                };
                await _unitOfWork.AuctionTransactions.AddAsync(payoutTx, cancellationToken);

                // Hoàn trả cọc cam kết bán cho người bán (nếu có)
                var sellerDeposit = trade.Auction?.AuctionRule?.SellerDepositAmount ?? 0m;
                if (sellerDeposit > 0 && sellerWallet.LockedBalance >= sellerDeposit)
                {
                    var releaseJournal = await _ledgerService.PostSellerListingDepositReleaseAsync(
                        sellerWallet.Id,
                        sellerDeposit,
                        trade.AuctionId,
                        cancellationToken);

                    var releaseTx = new AuctionTransaction
                    {
                        Id = Guid.NewGuid(),
                        WalletId = sellerWallet.Id,
                        AuctionId = trade.AuctionId,
                        AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_RELEASED,
                        TransactionStatus = TransactionStatus.COMPLETED,
                        Amount = sellerDeposit,
                        BalanceBefore = sellerWallet.AvailableBalance - sellerDeposit,
                        BalanceAfter = sellerWallet.AvailableBalance,
                        LedgerJournalId = releaseJournal.Id,
                        JournalCode = releaseJournal.JournalCode,
                        CreatedAt = now,
                        CompletedAt = now
                    };
                    await _unitOfWork.AuctionTransactions.AddAsync(releaseTx, cancellationToken);
                }
            }

            // 4. CHUYỂN TRẠNG THÁI HOÀN TẤT & BẮN SỰ KIỆN DOMAIN
            trade.CompleteTransfer();

            // Hủy tác vụ hẹn giờ tự động giải ngân
            await _delayQueue.CancelJobAsync(trade.AuctionId, AuctionDelayJobTypes.LIQUIDATION_DEADLINE, cancellationToken);

            var liqPdfUrl = trade.TransferContract?.LiquidationDocumentUrl;
            await _notificationService.SendNotificationAsync(
                userId: trade.SellerId,
                type: NotificationType.WALLET_TOPUP_SUCCESS,
                title: "Giao dịch bán SIM thành công - Tiền đã về ví!",
                content: $"Ban Quản Trị đã duyệt hồ sơ nghiệm thu SIM {trade.SimNumber?.PhoneNumber}. Bạn nhận được {netSellerPayout:N0} VNĐ vào ví (sau khi trừ phí sàn). Biên bản nghiệm thu và thanh lý đã được niêm phong chữ ký số PAdES X.509.",
                actionUrl: liqPdfUrl ?? $"/trade/{trade.Id}",
                referenceType: "TradeTransaction",
                referenceId: trade.Id,
                sendEmail: true,
                cancellationToken: cancellationToken);

            await _notificationService.SendNotificationAsync(
                userId: trade.BuyerId,
                type: NotificationType.SYSTEM_ALERT,
                title: "Chúc mừng bạn đã sở hữu SIM chính chủ!",
                content: $"Ban Quản Trị đã duyệt hoàn tất chuyển nhượng SIM {trade.SimNumber?.PhoneNumber}. Bạn đã trở thành chủ sở hữu chính thức. Biên bản nghiệm thu và thanh lý đã được niêm phong chữ ký số PAdES X.509.",
                actionUrl: liqPdfUrl ?? $"/trade/{trade.Id}",
                referenceType: "TradeTransaction",
                referenceId: trade.Id,
                sendEmail: true,
                cancellationToken: cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var finalEscrow = trade.Escrow?.EscrowAmount ?? 0m;
        var finalFeePercent = 0.05m;
        var finalFeeAmount = Math.Round(finalEscrow * finalFeePercent, 2);
        var finalNet = finalEscrow - finalFeeAmount;

        var allProofs = trade.SimTransfer.TransferProofs
            .OrderByDescending(p => p.CreatedDate)
            .Select(p => new TransferProofDto(
                p.Id,
                p.ImageUrl,
                p.Description,
                p.ProofStatus.ToString(),
                p.VerifiedAt,
                p.CreatedDate,
                p.ProofType.ToString()
            )).ToList();

        var statusMessage = trade.SimTransfer.SimTransferStatus == SimTransferStatus.COMPLETED
            ? "Đã duyệt hoàn tất cả 2 bên. Văn bản số 3 đã được niêm phong, SIM đã sang tên chính chủ và tiền đã giải ngân."
            : $"Đã xử lý duyệt hồ sơ cho {(isTargetSeller ? "Bên Bán" : "Bên Mua")}: {(request.IsApproved ? "CHẤP THUẬN" : "TỪ CHỐI")}.";

        return new SimTransferDetailsDto(
            TradeId: trade.Id,
            TradeTransactionCode: trade.TradeTransactionCode,
            SimTransferId: trade.SimTransfer.Id,
            SimTransferStatus: trade.SimTransfer.SimTransferStatus.ToString(),
            StartAt: trade.SimTransfer.StartAt,
            Deadline: trade.SimTransfer.Deadline,
            CancelledReason: trade.SimTransfer.CancelledReason,
            IsSeller: false,
            IsBuyer: false,
            CanSubmitProof: false,
            CanConfirmTransfer: false,
            SimPhoneNumber: trade.SimNumber?.PhoneNumber ?? "Chưa xác định",
            Proofs: allProofs,
            EscrowAmount: finalEscrow,
            EscrowStatus: (trade.Escrow?.EscrowStatus ?? EscrowStatus.HELD).ToString(),
            PlatformFeePercent: finalFeePercent,
            PlatformFeeAmount: finalFeeAmount,
            NetSellerPayout: finalNet,
            CompletedAt: trade.CompletedAt,
            LiquidationDocumentUrl: trade.TransferContract?.LiquidationDocumentUrl,
            Message: statusMessage
        );
    }
}
