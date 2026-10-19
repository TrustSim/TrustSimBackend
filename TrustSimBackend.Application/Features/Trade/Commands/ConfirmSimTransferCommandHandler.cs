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

        var now = DateTime.UtcNow;

        // 1. Cập nhật trạng thái chuyển nhượng và giao dịch
        trade.SimTransfer.SimTransferStatus = SimTransferStatus.COMPLETED;
        trade.TradeTransactionStatus = TradeTransactionStatus.Completed;
        trade.CompletedAt = now;

        // Duyệt toàn bộ bằng chứng chuyển nhượng đã nộp
        foreach (var p in trade.SimTransfer.TransferProofs)
        {
            p.ProofStatus = ProofStatus.APPROVED;
            p.VerifiedAt = now;
        }

        // 2. Chuyển giao quyền sở hữu SIM (SimOwnership)
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

        // 3. Khởi tạo, niêm phong chữ ký số PAdES và lưu trữ Văn bản 3: Biên bản nghiệm thu và thanh lý giao dịch điện tử
        if (trade.TransferContract != null && string.IsNullOrWhiteSpace(trade.TransferContract.LiquidationDocumentUrl))
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

        // 4. Giải ngân tiền Escrow qua sổ cái kép (Nợ 2030 / Có 2010 Seller + Có 4010 Sàn)
        var escrowAmount = trade.Escrow?.EscrowAmount ?? 0m;
        var platformFeePercent = 0.05m;
        var platformFeeAmount = Math.Round(escrowAmount * platformFeePercent, 2);
        var netSellerPayout = escrowAmount - platformFeeAmount;
        string? journalCode = null;

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

        var proofs = trade.SimTransfer.TransferProofs
            .OrderByDescending(p => p.CreatedDate)
            .Select(p => new TransferProofDto(
                p.Id,
                p.ImageUrl,
                p.Description,
                p.ProofStatus.ToString(),
                p.VerifiedAt,
                p.CreatedDate
            )).ToList();

        return new SimTransferDetailsDto(
            TradeId: trade.Id,
            TradeTransactionCode: trade.TradeTransactionCode,
            SimTransferId: trade.SimTransfer.Id,
            SimTransferStatus: trade.SimTransfer.SimTransferStatus.ToString(),
            StartAt: trade.SimTransfer.StartAt,
            Deadline: trade.SimTransfer.Deadline,
            CancelledReason: trade.SimTransfer.CancelledReason,
            IsSeller: false,
            IsBuyer: true,
            CanSubmitProof: false,
            CanConfirmTransfer: false,
            SimPhoneNumber: trade.SimNumber?.PhoneNumber ?? "Chưa xác định",
            Proofs: proofs,
            EscrowAmount: escrowAmount,
            EscrowStatus: (trade.Escrow?.EscrowStatus ?? EscrowStatus.RELEASED).ToString(),
            PlatformFeePercent: platformFeePercent,
            PlatformFeeAmount: platformFeeAmount,
            NetSellerPayout: netSellerPayout,
            CompletedAt: now,
            Message: $"Chúc mừng! Đã hoàn tất chuyển quyền sở hữu SIM và giải ngân thành công {netSellerPayout:N0} VNĐ về ví người bán."
        );
    }
}
