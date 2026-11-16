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
/// Handler xử lý Bên Mua xác nhận đã nhận SIM có sóng, kích hoạt sang tên chính chủ và giải ngân tiền Escrow.
/// </summary>
public class ConfirmSimTransferCommandHandler : IRequestHandler<ConfirmSimTransferCommand, SimTransferDetailsDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;
    private readonly INotificationService _notificationService;
    private readonly IPdfContractService _pdfContractService;
    private readonly IPdfSigningService _pdfSigningService;
    private readonly ICloudStorageService _cloudStorageService;
    private readonly IAuctionDelayQueueService _delayQueue;
    private readonly ILogger<ConfirmSimTransferCommandHandler> _logger;

    public ConfirmSimTransferCommandHandler(
        IUnitOfWork unitOfWork,
        ILedgerService ledgerService,
        INotificationService notificationService,
        IPdfContractService pdfContractService,
        IPdfSigningService pdfSigningService,
        ICloudStorageService cloudStorageService,
        IAuctionDelayQueueService delayQueue,
        ILogger<ConfirmSimTransferCommandHandler> logger)
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

    public async Task<SimTransferDetailsDto> Handle(ConfirmSimTransferCommand request, CancellationToken cancellationToken)
    {
        var trade = await _unitOfWork.TradeTransactions.GetByIdAsync(request.TradeId, cancellationToken);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch chuyển nhượng có ID '{request.TradeId}'.");
        }

        if (trade.BuyerId != request.UserId)
        {
            throw new ForbiddenAccessException("Chỉ có Người Mua mới có quyền bấm xác nhận nhận SIM.");
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

        var mergedProofs = SimTransferMappingHelper.GetMergedProofs(trade);
        var hasApprovedSellerProof = mergedProofs
            .Any(p => p.ProofType == TransferProofType.SELLER_HANDOVER && p.ProofStatus == ProofStatus.APPROVED);

        if (!hasApprovedSellerProof)
        {
            throw new BadRequestException("Bằng chứng chuyển nhượng của Người Bán phải được Ban Quản Trị sàn phê duyệt thành công trước khi Người Mua xác nhận nhận SIM.");
        }

        var now = DateTime.UtcNow;

        // 1. Cập nhật trạng thái chuyển nhượng và giao dịch
        trade.SimTransfer.SimTransferStatus = SimTransferStatus.COMPLETED;
        trade.TradeTransactionStatus = TradeTransactionStatus.Completed;
        trade.CompletedAt = now;

        // Chỉ duyệt các bộ bằng chứng đang ở trạng thái PENDING (giữ nguyên lịch sử REJECTED / WITHDRAWN)
        foreach (var p in mergedProofs.Where(p => p.ProofStatus == ProofStatus.PENDING))
        {
            p.ProofStatus = ProofStatus.APPROVED;
            p.VerifiedAt = now;
        }

        // 2. Chuyển giao quyền sở hữu SIM (SimOwnership, SimVerification & SimNumber.SimStatus)
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

        // 3. Khởi tạo, niêm phong chữ ký số PAdES và lưu trữ Văn bản 3: Biên bản nghiệm thu và thanh lý giao dịch điện tử
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
                _logger.LogInformation("[ConfirmSimTransfer] 📑 Sinh và niêm phong Biên bản nghiệm thu & thanh lý cho giao dịch {TradeCode}...", trade.TradeTransactionCode);
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
                    _logger.LogWarning(uploadEx, "[ConfirmSimTransfer] Lỗi upload Cloud, fallback lưu local file cho {TradeCode}", trade.TradeTransactionCode);
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

                _logger.LogInformation("[ConfirmSimTransfer] ✅ Đã lưu Biên bản thanh lý cho {TradeCode}: {Url} (Hash: {Hash})", trade.TradeTransactionCode, liqUrl, signResult.Sha256Hash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ConfirmSimTransfer] ⚠️ Lỗi trong quá trình sinh/ký biên bản thanh lý cho {TradeCode}. Vẫn tiếp tục giải ngân giao dịch.", trade.TradeTransactionCode);
            }
        }

        if (trade.TransferContract != null)
        {
            trade.TransferContract.ContractStatus = ContractStatus.COMPLETED;
        }

        // 4. Giải ngân tiền Escrow qua sổ cái kép & Mở khóa ví cả 2 bên
        var escrowAmount = trade.Escrow?.EscrowAmount ?? 0m;
        var platformFeePercent = 0.05m;
        var platformFeeAmount = Math.Round(escrowAmount * platformFeePercent, 2);
        var netSellerPayout = escrowAmount - platformFeeAmount;
        string? journalCode = null;

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

            journalCode = journal.JournalCode;
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

        // Mở khóa trạng thái ví Người Bán
        sellerWallet.WalletStatus = WalletStatus.ACTIVE;
        sellerWallet.FrozenAt = null;
        sellerWallet.FrozenReason = null;
        _unitOfWork.Wallets.Update(sellerWallet);

        // Mở khóa trạng thái ví Người Mua và giải phóng cọc còn kẹt nếu có
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

        // Hủy tác vụ hẹn giờ tự động giải ngân LIQUIDATION_DEADLINE vì Người Mua đã chủ động nghiệm thu
        await _delayQueue.CancelJobAsync(trade.AuctionId, AuctionDelayJobTypes.LIQUIDATION_DEADLINE, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[SimTransfer Completed] ✅ Giao dịch {TradeCode} hoàn tất thành công! Sim {SimNumber} đã chuyển cho Người mua {BuyerId}. Escrow {Total:N0} VNĐ đã giải ngân cho Người bán {SellerId} (Thực nhận: {Net:N0} VNĐ, Phí: {Fee:N0} VNĐ, Chứng từ #{JournalCode})",
            trade.TradeTransactionCode, trade.SimNumber?.PhoneNumber, trade.BuyerId, trade.SellerId, escrowAmount, netSellerPayout, platformFeeAmount, journalCode ?? "N/A");

        // 4. Bắn thông báo hoàn tất cho cả 2 bên (Action URL trỏ trực tiếp tới Biên Bản Thanh Lý PDF trên Supabase CDN)
        var liqPdfUrl = trade.TransferContract?.LiquidationDocumentUrl;
        await _notificationService.SendNotificationAsync(
            userId: trade.SellerId,
            type: NotificationType.WALLET_TOPUP_SUCCESS,
            title: "Giao dịch bán SIM thành công - Tiền đã về ví!",
            content: $"Người mua đã xác nhận nhận SIM {trade.SimNumber?.PhoneNumber}. Bạn nhận được {netSellerPayout:N0} VNĐ vào ví (sau khi trừ phí sàn). Biên bản nghiệm thu và thanh lý đã được niêm phong chữ ký số PAdES X.509.",
            actionUrl: liqPdfUrl ?? $"/trade/{trade.Id}",
            referenceType: "TradeTransaction",
            referenceId: trade.Id,
            sendEmail: true,
            cancellationToken: cancellationToken);

        await _notificationService.SendNotificationAsync(
            userId: trade.BuyerId,
            type: NotificationType.SYSTEM_ALERT,
            title: "Chúc mừng bạn đã sở hữu SIM chính chủ!",
            content: $"Giao dịch chuyển nhượng SIM {trade.SimNumber?.PhoneNumber} đã hoàn tất. Bạn đã trở thành chủ sở hữu chính thức. Biên bản nghiệm thu và thanh lý đã được niêm phong chữ ký số PAdES X.509.",
            actionUrl: liqPdfUrl ?? $"/trade/{trade.Id}",
            referenceType: "TradeTransaction",
            referenceId: trade.Id,
            sendEmail: true,
            cancellationToken: cancellationToken);

        return SimTransferMappingHelper.BuildSimTransferDetailsDto(
            trade,
            request.UserId,
            isAdmin: false,
            message: $"Chúc mừng! Đã hoàn tất chuyển quyền sở hữu SIM và giải ngân thành công {netSellerPayout:N0} VNĐ về ví người bán.");
    }
}
