using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Application.Features.Trade.Helpers;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Trade.Commands;

/// <summary>
/// Handler xử lý Admin duyệt hoặc từ chối một bộ bằng chứng (TransferProof) cụ thể,
/// tự động niêm phong Văn bản số 3 và cập nhật quyền sở hữu SIM khi đủ cả 2 bên.
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
        var targetProof = await _unitOfWork.TradeTransactions.GetTransferProofByIdAsync(request.TransferProofId, cancellationToken);
        if (targetProof == null)
        {
            throw new NotFoundException($"Không tìm thấy bộ bằng chứng có ID '{request.TransferProofId}'.");
        }

        var trade = await _unitOfWork.TradeTransactions.GetByAuctionIdAsync(targetProof.AuctionId, cancellationToken);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch chuyển nhượng cho phiên đấu giá '{targetProof.AuctionId}'.");
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

        if (targetProof.ProofStatus != ProofStatus.PENDING)
        {
            throw new BadRequestException($"Bộ bằng chứng này đang ở trạng thái '{targetProof.ProofStatus}', chỉ có thể duyệt/từ chối hồ sơ đang chờ (PENDING).");
        }

        // Lấy instance đang được tracking trong trade nếu có để tránh lệch state khi SaveChanges
        var trackedProof = SimTransferMappingHelper.GetMergedProofs(trade)
            .FirstOrDefault(p => p.Id == targetProof.Id) ?? targetProof;

        var isTargetSeller = trackedProof.ProofType == TransferProofType.SELLER_HANDOVER;
        var allProofs = SimTransferMappingHelper.GetMergedProofs(trade);
        var now = DateTime.UtcNow;

        if (!isTargetSeller)
        {
            var sellerProofs = allProofs.Where(p => p.ProofType == TransferProofType.SELLER_HANDOVER).ToList();
            var hasApprovedSellerProof = sellerProofs.Any(p => p.ProofStatus == ProofStatus.APPROVED);
            var hasPendingSellerProof = sellerProofs.Any(p => p.ProofStatus == ProofStatus.PENDING);
            var isSellerStatusInvalid = trade.SimTransfer.SimTransferStatus == SimTransferStatus.SELLER_PROOF_SUBMITTED ||
                                        trade.SimTransfer.SimTransferStatus == SimTransferStatus.PENDING_SELLER_ACTION;

            if (sellerProofs.Count == 0 || !hasApprovedSellerProof || hasPendingSellerProof || isSellerStatusInvalid)
            {
                throw new BadRequestException("Phải phê duyệt bằng chứng chuyển nhượng của Người Bán trước khi duyệt bằng chứng nhận SIM của Người Mua.");
            }
        }

        if (request.IsApproved)
        {
            trackedProof.ProofStatus = ProofStatus.APPROVED;
            trackedProof.RejectionReason = null;
            trackedProof.VerifiedAt = now;
            trackedProof.VerifiedByAdminId = request.AdminUserId;

            if (isTargetSeller)
            {
                trade.SimTransfer.SimTransferStatus = SimTransferStatus.PENDING_BUYER_ACTION;

                _logger.LogInformation(
                    "[Admin Review] Admin {AdminId} đã DUYỆT bộ bằng chứng {ProofId} của Bên Bán cho giao dịch {TradeCode}",
                    request.AdminUserId, trackedProof.Id, trade.TradeTransactionCode);

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
                _logger.LogInformation(
                    "[Admin Review] Admin {AdminId} đã DUYỆT bộ bằng chứng {ProofId} của Bên Mua cho giao dịch {TradeCode}",
                    request.AdminUserId, trackedProof.Id, trade.TradeTransactionCode);
            }
        }
        else
        {
            var reasonText = string.IsNullOrWhiteSpace(request.RejectionReason)
                ? "Hồ sơ không hợp lệ"
                : request.RejectionReason.Trim();

            trackedProof.ProofStatus = ProofStatus.REJECTED;
            trackedProof.RejectionReason = reasonText;
            trackedProof.VerifiedAt = now;
            trackedProof.VerifiedByAdminId = request.AdminUserId;

            if (isTargetSeller)
            {
                trade.SimTransfer.SimTransferStatus = SimTransferStatus.PENDING_SELLER_ACTION;

                _logger.LogWarning(
                    "[Admin Review] Admin {AdminId} đã TỪ CHỐI bộ bằng chứng {ProofId} của Bên Bán cho {TradeCode}. Lý do: {Reason}",
                    request.AdminUserId, trackedProof.Id, trade.TradeTransactionCode, reasonText);

                await _notificationService.SendNotificationAsync(
                    trade.SellerId,
                    NotificationType.TRADE_TRANSFERRING_SIM,
                    "Biên bản sang tên SIM bị từ chối",
                    $"Hồ sơ sang tên cho SIM {trade.SimNumber?.PhoneNumber} bị từ chối với lý do: {reasonText}. Vui lòng nộp lại biên bản chính xác.",
                    $"/trade/{trade.Id}",
                    cancellationToken: cancellationToken);
            }
            else
            {
                trade.SimTransfer.SimTransferStatus = SimTransferStatus.PENDING_BUYER_ACTION;

                _logger.LogWarning(
                    "[Admin Review] Admin {AdminId} đã TỪ CHỐI bộ bằng chứng {ProofId} của Bên Mua cho {TradeCode}. Lý do: {Reason}",
                    request.AdminUserId, trackedProof.Id, trade.TradeTransactionCode, reasonText);

                await _notificationService.SendNotificationAsync(
                    trade.BuyerId,
                    NotificationType.TRADE_TRANSFERRING_SIM,
                    "Ảnh nghiệm thu SIM bị từ chối",
                    $"Ảnh nghiệm thu SIM {trade.SimNumber?.PhoneNumber} bị từ chối với lý do: {reasonText}. Vui lòng tải lại ảnh kiểm tra sóng / app nhà mạng.",
                    $"/trade/{trade.Id}",
                    cancellationToken: cancellationToken);
            }
        }

        // Kiểm tra điều kiện hoàn tất cả 2 bên
        var isSellerApproved = allProofs.Any(p => p.ProofType == TransferProofType.SELLER_HANDOVER && p.ProofStatus == ProofStatus.APPROVED)
            || (trackedProof.ProofType == TransferProofType.SELLER_HANDOVER && trackedProof.ProofStatus == ProofStatus.APPROVED);
        var isBuyerApproved = allProofs.Any(p => p.ProofType == TransferProofType.BUYER_RECEIPT && p.ProofStatus == ProofStatus.APPROVED)
            || (trackedProof.ProofType == TransferProofType.BUYER_RECEIPT && trackedProof.ProofStatus == ProofStatus.APPROVED);

        if (request.IsApproved && isSellerApproved && isBuyerApproved)
        {
            await CompleteTransferAndReleaseEscrowAsync(trade, now, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var statusMessage = trade.SimTransfer.SimTransferStatus == SimTransferStatus.COMPLETED
            ? "Đã duyệt hoàn tất cả 2 bên. Văn bản số 3 đã được niêm phong, SIM đã sang tên chính chủ và tiền đã giải ngân."
            : $"Đã xử lý duyệt hồ sơ cho {(isTargetSeller ? "Bên Bán" : "Bên Mua")}: {(request.IsApproved ? "CHẤP THUẬN" : "TỪ CHỐI")}.";

        return SimTransferMappingHelper.BuildSimTransferDetailsDto(trade, request.AdminUserId, isAdmin: true, statusMessage);
    }

    private async Task CompleteTransferAndReleaseEscrowAsync(
        TradeTransaction trade,
        DateTime now,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[Admin Review] 🎯 Giao dịch {TradeCode} đã DUYỆT CẢ 2 BÊN! Kích hoạt sinh Văn Bản 3, chuyển sở hữu SIM và giải ngân Escrow...",
            trade.TradeTransactionCode);

        // 1. TẠO VĂN BẢN SỐ 3 (Biên bản nghiệm thu & thanh lý chuyển nhượng SIM)
        if (trade.TransferContract == null)
        {
            trade.TransferContract = new TransferContract
            {
                Id = Guid.NewGuid(),
                TradeTransactionId = trade.Id,
                ContractStatus = ContractStatus.FULLY_SIGNED,
                DocumentUrl = string.Empty,
                DocumentHash = string.Empty
            };
        }

        if (string.IsNullOrWhiteSpace(trade.TransferContract.LiquidationDocumentUrl))
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
                string liqUrl;
                try
                {
                    liqUrl = await _cloudStorageService.UploadImageBytesAsync(
                        signResult.SignedPdfBytes,
                        liqFileName,
                        "contracts",
                        "application/pdf",
                        cancellationToken);
                }
                catch (Exception uploadEx)
                {
                    _logger.LogWarning(uploadEx, "[Admin Review] Lỗi upload Cloud, fallback lưu local file cho {TradeCode}", trade.TradeTransactionCode);
                    var localDir = Path.Combine(AppContext.BaseDirectory, "uploads", "contracts");
                    Directory.CreateDirectory(localDir);
                    var localPath = Path.Combine(localDir, liqFileName);
                    await File.WriteAllBytesAsync(localPath, signResult.SignedPdfBytes, cancellationToken);
                    liqUrl = $"/uploads/contracts/{liqFileName}";
                }

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

        // 2. CẬP NHẬT TOÀN BỘ QUYỀN SỞ HỮU SIM: CHỦ CŨ -> ENDED, CHỦ MỚI -> ACTIVE
        var activeOwnerships = (await _unitOfWork.SimOwnerships.GetActiveOwnershipsBySimNumberIdAsync(trade.SimNumberId, cancellationToken))?.ToList() ?? new List<SimOwnership>();
        if (activeOwnerships.Count == 0)
        {
            var singleActive = await _unitOfWork.SimOwnerships.GetActiveOwnershipBySimNumberIdAsync(trade.SimNumberId, cancellationToken);
            if (singleActive != null)
            {
                activeOwnerships.Add(singleActive);
            }
        }

        foreach (var oldOwnership in activeOwnerships.Where(o => o.UserAccountId != trade.BuyerId))
        {
            oldOwnership.OwnershipStatus = OwnershipStatus.ENDED;
            _unitOfWork.SimOwnerships.Update(oldOwnership);
        }

        var existingBuyerOwnership = activeOwnerships.FirstOrDefault(o => o.UserAccountId == trade.BuyerId);
        if (existingBuyerOwnership == null)
        {
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
        }
        else
        {
            existingBuyerOwnership.OwnershipStatus = OwnershipStatus.ACTIVE;
            _unitOfWork.SimOwnerships.Update(existingBuyerOwnership);
        }

        if (trade.SimNumber != null)
        {
            trade.SimNumber.SimStatus = SimStatus.TRANSFERRED;
            _unitOfWork.SimNumbers.Update(trade.SimNumber);
        }

        // 3. GIẢI NGÂN QUỸ ESCROW & MỞ ĐÓNG BĂNG VÍ CẢ 2 BÊN
        var escrowAmount = trade.Escrow?.EscrowAmount ?? 0m;
        var platformFeeAmount = Math.Round(escrowAmount * SimTransferMappingHelper.DefaultPlatformFeePercent, 2);
        var netSellerPayout = escrowAmount - platformFeeAmount;

        var sellerWallet = await _unitOfWork.Wallets.GetByUserIdAsync(trade.SellerId, cancellationToken);
        if (sellerWallet == null)
        {
            throw new NotFoundException($"Không tìm thấy ví tiền của người bán ID '{trade.SellerId}'.");
        }

        if (sellerWallet.WalletStatus == WalletStatus.LOCKED)
        {
            throw new BadRequestException("Ví tiền người bán đang bị khóa kỷ luật, không thể nhận tiền giải ngân.");
        }

        if (trade.Escrow != null && (trade.Escrow.EscrowStatus == EscrowStatus.HELD || trade.Escrow.EscrowStatus == EscrowStatus.FROZEN) && escrowAmount > 0)
        {
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

        sellerWallet.WalletStatus = WalletStatus.ACTIVE;
        sellerWallet.FrozenAt = null;
        sellerWallet.FrozenReason = null;
        _unitOfWork.Wallets.Update(sellerWallet);

        var buyerWallet = await _unitOfWork.Wallets.GetByUserIdAsync(trade.BuyerId, cancellationToken);
        if (buyerWallet != null)
        {
            buyerWallet.WalletStatus = WalletStatus.ACTIVE;
            buyerWallet.FrozenAt = null;
            buyerWallet.FrozenReason = null;

            if (trade.AuctionId != Guid.Empty)
            {
                var participant = await _unitOfWork.Auctions.GetParticipantAsync(trade.AuctionId, trade.BuyerId, cancellationToken);
                if (participant != null)
                {
                    var remainingLockedDeposits = participant.Deposits
                        .Where(d => d.DepositStatus == DepositStatus.LOCKED)
                        .ToList();

                    foreach (var dep in remainingLockedDeposits)
                    {
                        if (buyerWallet.LockedBalance >= dep.DepositAmount)
                        {
                            dep.DepositStatus = DepositStatus.RELEASED;
                            dep.ReleasedAT = now;

                            var relJournal = await _ledgerService.PostAuctionDepositReleaseAsync(
                                buyerWallet.Id,
                                dep.DepositAmount,
                                dep.Id,
                                cancellationToken);

                            var refundTx = new AuctionTransaction
                            {
                                Id = Guid.NewGuid(),
                                WalletId = buyerWallet.Id,
                                AuctionId = trade.AuctionId,
                                AuctionDeposit = dep,
                                AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_RELEASED,
                                TransactionStatus = TransactionStatus.COMPLETED,
                                Amount = dep.DepositAmount,
                                BalanceBefore = buyerWallet.AvailableBalance,
                                BalanceAfter = buyerWallet.AvailableBalance,
                                LedgerJournalId = relJournal.Id,
                                JournalCode = relJournal.JournalCode,
                                CreatedAt = now,
                                CompletedAt = now
                            };
                            await _unitOfWork.AuctionTransactions.AddAsync(refundTx, cancellationToken);
                        }
                    }
                }
            }

            _unitOfWork.Wallets.Update(buyerWallet);
        }

        // 4. CHUYỂN TRẠNG THÁI HOÀN TẤT & BẮN SỰ KIỆN DOMAIN
        trade.CompleteTransfer();

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
}
