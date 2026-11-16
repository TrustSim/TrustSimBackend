using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Infrastructure.Persistence;

namespace TrustSimBackend.Infrastructure.Workers.Auction.DelayJobs;

/// <summary>
/// Xử lý các mốc quá hạn hợp đồng và chuyển nhượng SIM hậu thanh toán: SIGNING_DEADLINE, TRANSFER_DEADLINE, LIQUIDATION_DEADLINE.
/// </summary>
public sealed class TradeDeadlineJobHandler : IAuctionDelayJobHandler
{
    private readonly TrustSimDbContext _context;
    private readonly ILedgerService _ledgerService;
    private readonly SellerListingDepositHelper _depositHelper;
    private readonly ILogger<TradeDeadlineJobHandler> _logger;
    private readonly IConnectionMultiplexer? _redis;
    private readonly INotificationService? _notificationService;
    private readonly IPdfContractService? _pdfContractService;
    private readonly IPdfSigningService? _pdfSigningService;
    private readonly ICloudStorageService? _cloudStorageService;

    public TradeDeadlineJobHandler(
        TrustSimDbContext context,
        ILedgerService ledgerService,
        SellerListingDepositHelper depositHelper,
        ILogger<TradeDeadlineJobHandler> logger,
        IConnectionMultiplexer? redis = null,
        INotificationService? notificationService = null,
        IPdfContractService? pdfContractService = null,
        IPdfSigningService? pdfSigningService = null,
        ICloudStorageService? cloudStorageService = null)
    {
        _context = context;
        _ledgerService = ledgerService;
        _depositHelper = depositHelper;
        _logger = logger;
        _redis = redis;
        _notificationService = notificationService;
        _pdfContractService = pdfContractService;
        _pdfSigningService = pdfSigningService;
        _cloudStorageService = cloudStorageService;
    }

    public bool CanHandle(string jobType)
        => jobType == AuctionDelayJobTypes.SIGNING_DEADLINE
        || jobType == AuctionDelayJobTypes.TRANSFER_DEADLINE
        || jobType == AuctionDelayJobTypes.LIQUIDATION_DEADLINE;

    public Task HandleAsync(AuctionDelayJob job, CancellationToken cancellationToken) => job.JobType switch
    {
        AuctionDelayJobTypes.SIGNING_DEADLINE => HandleSigningDeadlineAsync(job, cancellationToken),
        AuctionDelayJobTypes.TRANSFER_DEADLINE => HandleTransferDeadlineAsync(job, cancellationToken),
        AuctionDelayJobTypes.LIQUIDATION_DEADLINE => HandleLiquidationDeadlineAsync(job, cancellationToken),
        _ => Task.CompletedTask
    };

    private async Task HandleSigningDeadlineAsync(AuctionDelayJob job, CancellationToken cancellationToken)
    {
        Guid.TryParse(job.ExtraData, out var tradeId);
        var trade = await _context.TradeTransactions
            .Include(t => t.TransferContract)
                .ThenInclude(c => c!.DigitalSignatures)
            .Include(t => t.SimNumber)
            .Include(t => t.Escrow)
            .Include(t => t.Buyer)
                .ThenInclude(b => b!.Wallet)
            .Include(t => t.Seller)
                .ThenInclude(s => s!.Wallet)
            .Include(t => t.Auction)
                .ThenInclude(a => a!.AuctionRule)
            .FirstOrDefaultAsync(t => (tradeId != Guid.Empty && t.Id == tradeId) || (tradeId == Guid.Empty && t.AuctionId == job.AuctionId), cancellationToken);

        if (trade == null || trade.TransferContract == null || trade.TransferContract.ContractStatus == ContractStatus.FULLY_SIGNED)
        {
            return;
        }

        var buyerSig = trade.TransferContract.DigitalSignatures.FirstOrDefault(s => s.SignatureRole == SignatureRole.BUYER);
        var sellerSig = trade.TransferContract.DigitalSignatures.FirstOrDefault(s => s.SignatureRole == SignatureRole.SELLER);

        // Kịch bản 1: Người mua ĐÃ ký, Người bán KHÔNG ký -> Kích hoạt Cưỡng chế Ủy quyền
        if (buyerSig != null && sellerSig == null)
        {
            _logger.LogWarning("[PostAuctionWorker] ⚖️ Hết hạn ký: Seller {SellerId} không ký hợp đồng. Kích hoạt Cưỡng chế Ủy quyền!", trade.SellerId);

            if (_redis != null && _redis.IsConnected)
            {
                var db = _redis.GetDatabase();
                await db.StreamAddAsync("stream:contracts:sign", new NameValueEntry[]
                {
                    new("tradeId", trade.Id.ToString()),
                    new("isAutoEnforced", "true")
                });
            }

            if (_notificationService != null)
            {
                await _notificationService.SendNotificationAsync(
                    userId: trade.SellerId,
                    type: NotificationType.SELLER_SIGNING_TIMEOUT_AUTO_ENFORCED,
                    title: "⚠️ Quá hạn ký hợp đồng bán SIM",
                    content: $"Thời hạn 24h ký hợp đồng SIM {trade.SimNumber?.PhoneNumber} đã hết. Hệ thống đã kích hoạt niêm phong cưỡng chế đại diện. Vui lòng tiến hành sang tên đúng hạn.",
                    referenceType: "TradeTransaction",
                    referenceId: trade.Id,
                    sendEmail: true,
                    cancellationToken: cancellationToken);
            }
        }
        // Kịch bản 2: Người mua KHÔNG ký -> Hủy giao dịch, hoàn cọc Seller
        else if (buyerSig == null)
        {
            _logger.LogWarning("[PostAuctionWorker] ❌ Hết hạn ký: Buyer {BuyerId} không ký hợp đồng. Hủy giao dịch!", trade.BuyerId);

            trade.TradeTransactionStatus = TradeTransactionStatus.Cancelled_SigningTimeout;
            trade.CancelledAt = DateTime.UtcNow;
            trade.CancelledReason = "Người mua không hoàn tất ký hợp đồng điện tử trong thời hạn quy định.";

            if (trade.TransferContract != null)
            {
                trade.TransferContract.ContractStatus = ContractStatus.DRAFT;
            }

            if (trade.SimNumber != null)
            {
                trade.SimNumber.SimStatus = SimStatus.AVAILABLE;
            }

            if (trade.Seller?.Wallet != null && trade.Auction?.AuctionRule != null)
            {
                await _depositHelper.TryReleaseSellerListingDepositAsync(
                    _context,
                    _ledgerService,
                    trade.Seller.Wallet,
                    trade.AuctionId,
                    trade.Auction.AuctionRule.SellerDepositAmount,
                    DateTime.UtcNow,
                    cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task HandleTransferDeadlineAsync(AuctionDelayJob job, CancellationToken cancellationToken)
    {
        Guid.TryParse(job.ExtraData, out var tradeId);
        var trade = await _context.TradeTransactions
            .Include(t => t.TransferContract)
            .Include(t => t.SimTransfer)
                .ThenInclude(st => st!.TransferProofs)
                    .ThenInclude(tp => tp.Images)
            .Include(t => t.SimNumber)
            .Include(t => t.Escrow)
            .Include(t => t.Buyer)
                .ThenInclude(b => b!.Wallet)
            .Include(t => t.Seller)
                .ThenInclude(s => s!.Wallet)
            .Include(t => t.Auction)
                .ThenInclude(a => a!.AuctionRule)
            .FirstOrDefaultAsync(t => (tradeId != Guid.Empty && t.Id == tradeId) || (tradeId == Guid.Empty && t.AuctionId == job.AuctionId), cancellationToken);

        if (trade == null || trade.TradeTransactionStatus != TradeTransactionStatus.PendingTransfer)
        {
            return;
        }

        var hasProof = trade.SimTransfer?.TransferProofs
            .Any(p => p.ProofStatus == ProofStatus.PENDING || p.ProofStatus == ProofStatus.APPROVED) ?? false;
        if (hasProof)
        {
            return;
        }

        var now = DateTime.UtcNow;
        _logger.LogWarning("[PostAuctionWorker] 🚨 Hết hạn chuyển nhượng SIM {TradeCode}: Người bán {SellerId} không bàn giao SIM.",
            trade.TradeTransactionCode, trade.SellerId);

        trade.TradeTransactionStatus = TradeTransactionStatus.Cancelled_TransferTimeout;
        trade.CancelledAt = now;
        trade.CancelledReason = "Người bán không thực hiện nghĩa vụ bàn giao và sang tên số thuê bao trong thời hạn quy định.";

        if (trade.SimTransfer != null)
        {
            trade.SimTransfer.SimTransferStatus = SimTransferStatus.CANCELLED;
            trade.SimTransfer.CancelledReason = trade.CancelledReason;
        }

        if (trade.SimNumber != null)
        {
            trade.SimNumber.SimStatus = SimStatus.AVAILABLE;
        }

        if (trade.Escrow != null && trade.Escrow.EscrowStatus == EscrowStatus.HELD && trade.Buyer?.Wallet != null)
        {
            var escrowRefundJournal = await _ledgerService.PostEscrowRefundAsync(
                trade.Buyer.Wallet.Id,
                trade.Escrow.EscrowAmount,
                trade.Escrow.Id,
                cancellationToken);
            trade.Escrow.EscrowStatus = EscrowStatus.REFUNDED;

            var escrowRefundTx = new AuctionTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = trade.Buyer.Wallet.Id,
                AuctionId = trade.AuctionId,
                EscrowId = trade.Escrow.Id,
                AuctionTransactionType = AuctionTransactionType.ESCROW_REFUNDED,
                TransactionStatus = TransactionStatus.COMPLETED,
                Amount = trade.Escrow.EscrowAmount,
                BalanceBefore = trade.Buyer.Wallet.AvailableBalance - trade.Escrow.EscrowAmount,
                BalanceAfter = trade.Buyer.Wallet.AvailableBalance,
                LedgerJournalId = escrowRefundJournal.Id,
                JournalCode = escrowRefundJournal.JournalCode,
                CreatedAt = now,
                CompletedAt = now
            };
            await _context.AuctionTransactions.AddAsync(escrowRefundTx, cancellationToken);
        }

        var sellerDeposit = trade.Auction?.AuctionRule?.SellerDepositAmount ?? 0m;
        await _depositHelper.TryForfeitSellerListingDepositAsync(
            _context,
            _ledgerService,
            trade.Seller?.Wallet,
            trade.Buyer?.Wallet,
            trade.AuctionId,
            trade.Id,
            sellerDeposit,
            now,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        if (_notificationService != null)
        {
            await _notificationService.SendNotificationAsync(
                userId: trade.BuyerId,
                type: NotificationType.SYSTEM_ALERT,
                title: "⚠️ Giao dịch chuyển nhượng SIM đã bị hủy do Người bán quá hạn",
                content: $"Người bán đã quá thời hạn bàn giao SIM {trade.SimNumber?.PhoneNumber}. Hệ thống đã hoàn trả 100% tiền Escrow và bồi thường 50% tiền cọc của Người bán vào ví của bạn.",
                referenceType: "TradeTransaction",
                referenceId: trade.Id,
                sendEmail: true,
                cancellationToken: cancellationToken);

            await _notificationService.SendNotificationAsync(
                userId: trade.SellerId,
                type: NotificationType.SYSTEM_ALERT,
                title: "🚨 Vi phạm thời hạn bàn giao SIM",
                content: $"Bạn đã quá hạn thực hiện thủ tục sang tên SIM {trade.SimNumber?.PhoneNumber}. Giao dịch đã bị hủy và bạn bị xử lý chế tài tịch thu tiền cọc theo Quy chế Sàn TrustSim.",
                referenceType: "TradeTransaction",
                referenceId: trade.Id,
                sendEmail: true,
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleLiquidationDeadlineAsync(AuctionDelayJob job, CancellationToken cancellationToken)
    {
        Guid.TryParse(job.ExtraData, out var tradeId);
        var trade = await _context.TradeTransactions
            .Include(t => t.TransferContract)
            .Include(t => t.SimTransfer)
                .ThenInclude(st => st!.TransferProofs)
                    .ThenInclude(tp => tp.Images)
            .Include(t => t.SimNumber)
            .Include(t => t.Escrow)
            .Include(t => t.Buyer)
                .ThenInclude(b => b!.Wallet)
            .Include(t => t.Seller)
                .ThenInclude(s => s!.Wallet)
            .Include(t => t.Auction)
                .ThenInclude(a => a!.AuctionRule)
            .FirstOrDefaultAsync(t => (tradeId != Guid.Empty && t.Id == tradeId) || (tradeId == Guid.Empty && t.AuctionId == job.AuctionId), cancellationToken);

        if (trade == null || trade.TradeTransactionStatus != TradeTransactionStatus.PendingTransfer)
        {
            return;
        }

        var hasProof = trade.SimTransfer?.TransferProofs.Any(p => p.ProofStatus == ProofStatus.PENDING || p.ProofStatus == ProofStatus.APPROVED) ?? false;
        if (!hasProof)
        {
            return;
        }

        _logger.LogInformation("[PostAuctionWorker] ⏱️ Hết hạn nghiệm thu im lặng cho giao dịch {TradeCode}. Kích hoạt Tự Động Nghiệm Thu & Giải Ngân!", trade.TradeTransactionCode);

        var now = DateTime.UtcNow;

        if (trade.SimTransfer != null)
        {
            trade.SimTransfer.SimTransferStatus = SimTransferStatus.COMPLETED;
            foreach (var p in trade.SimTransfer.TransferProofs.Where(p => p.ProofStatus == ProofStatus.PENDING))
            {
                p.ProofStatus = ProofStatus.APPROVED;
                p.VerifiedAt = now;
            }
        }

        trade.TradeTransactionStatus = TradeTransactionStatus.Completed;
        trade.CompletedAt = now;

        var activeOwnerships = await _context.SimOwnerships
            .Where(o => o.SimNumberId == trade.SimNumberId && o.OwnershipStatus == OwnershipStatus.ACTIVE)
            .ToListAsync(cancellationToken);
        foreach (var oldOwnership in activeOwnerships.Where(o => o.UserAccountId != trade.BuyerId))
        {
            oldOwnership.OwnershipStatus = OwnershipStatus.ENDED;
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
            await _context.SimOwnerships.AddAsync(newBuyerOwnership, cancellationToken);

            var verification = new SimVerification
            {
                Id = Guid.NewGuid(),
                SimOwnershipId = newBuyerOwnership.Id,
                VerificationStatus = VerificationStatus.VERIFIED,
                VerificationMethod = "AUCTION_TRANSFER_AUTO_LIQUIDATED",
                RequestedAt = now,
                VerifiedAt = now
            };
            await _context.SimVerifications.AddAsync(verification, cancellationToken);
        }
        else
        {
            existingBuyerOwnership.OwnershipStatus = OwnershipStatus.ACTIVE;
        }

        if (trade.SimNumber != null)
        {
            trade.SimNumber.SimStatus = SimStatus.TRANSFERRED;
        }

        var escrowAmount = trade.Escrow?.EscrowAmount ?? 0m;
        var platformFeeAmount = Math.Round(escrowAmount * 0.05m, 2);
        var netSellerPayout = escrowAmount - platformFeeAmount;

        if (trade.Escrow != null && trade.Escrow.EscrowStatus == EscrowStatus.HELD && trade.Seller?.Wallet != null && escrowAmount > 0)
        {
            var journal = await _ledgerService.PostEscrowReleaseAsync(
                trade.Seller.Wallet.Id,
                escrowAmount,
                platformFeeAmount,
                trade.Escrow.Id,
                cancellationToken);
            trade.Escrow.EscrowStatus = EscrowStatus.RELEASED;
            trade.Escrow.ReleasedAt = now;

            var payoutTx = new AuctionTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = trade.Seller.Wallet.Id,
                AuctionId = trade.AuctionId,
                EscrowId = trade.Escrow.Id,
                AuctionTransactionType = AuctionTransactionType.SELLER_PAYOUT,
                TransactionStatus = TransactionStatus.COMPLETED,
                Amount = netSellerPayout,
                BalanceBefore = trade.Seller.Wallet.AvailableBalance - netSellerPayout,
                BalanceAfter = trade.Seller.Wallet.AvailableBalance,
                LedgerJournalId = journal.Id,
                JournalCode = journal.JournalCode,
                CreatedAt = now,
                CompletedAt = now
            };
            await _context.AuctionTransactions.AddAsync(payoutTx, cancellationToken);

            var sellerDeposit = trade.Auction?.AuctionRule?.SellerDepositAmount ?? 0m;
            await _depositHelper.TryReleaseSellerListingDepositAsync(
                _context,
                _ledgerService,
                trade.Seller.Wallet,
                trade.AuctionId,
                sellerDeposit,
                now,
                cancellationToken);

            if (trade.Seller?.Wallet != null)
            {
                trade.Seller.Wallet.WalletStatus = WalletStatus.ACTIVE;
                trade.Seller.Wallet.FrozenAt = null;
                trade.Seller.Wallet.FrozenReason = null;
            }

            if (trade.Buyer?.Wallet != null)
            {
                trade.Buyer.Wallet.WalletStatus = WalletStatus.ACTIVE;
                trade.Buyer.Wallet.FrozenAt = null;
                trade.Buyer.Wallet.FrozenReason = null;
            }
        }

        if (trade.TransferContract != null)
        {
            if (string.IsNullOrWhiteSpace(trade.TransferContract.LiquidationDocumentUrl)
                && _pdfContractService != null
                && _pdfSigningService != null
                && _cloudStorageService != null)
            {
                try
                {
                    var rawLiqBytes = await _pdfContractService.GenerateLiquidationMinutePdfAsync(trade.Id, cancellationToken);
                    var stampBoxes = await _pdfContractService.GetStampBoxesAsync(trade.Id, trade.TransferContract.IsAutoEnforced, cancellationToken);

                    var signResult = await _pdfSigningService.SignPdfPadesAsync(
                        rawLiqBytes,
                        "Nghiệm thu tự động hoàn tất chuyển quyền sử dụng SIM và thanh lý giao dịch điện tử",
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
                    trade.TransferContract.BlockchainTxHash = $"0x{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{trade.TradeTransactionCode}_{signResult.Sha256Hash}_{signResult.SignedAt:O}"))).ToLowerInvariant()}";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[PostAuctionWorker] ⚠️ Lỗi khi sinh/ký biên bản thanh lý tự động cho {TradeCode}.", trade.TradeTransactionCode);
                }
            }

            trade.TransferContract.LiquidationSealedAt ??= now;
            trade.TransferContract.ContractStatus = ContractStatus.COMPLETED;
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (_notificationService != null)
        {
            await _notificationService.SendNotificationAsync(
                userId: trade.SellerId,
                type: NotificationType.WALLET_TOPUP_SUCCESS,
                title: "Giao dịch bán SIM tự động hoàn tất - Tiền đã về ví!",
                content: $"Người mua đã quá thời hạn phản hồi. Hệ thống tự động nghiệm thu và giải ngân {netSellerPayout:N0} VNĐ về ví khả dụng của bạn cho SIM {trade.SimNumber?.PhoneNumber}.",
                actionUrl: $"/trade/{trade.Id}",
                referenceType: "TradeTransaction",
                referenceId: trade.Id,
                sendEmail: true,
                cancellationToken: cancellationToken);

            await _notificationService.SendNotificationAsync(
                userId: trade.BuyerId,
                type: NotificationType.SYSTEM_ALERT,
                title: "Giao dịch chuyển nhượng SIM đã tự động hoàn tất",
                content: $"Hệ thống đã tự động nghiệm thu giao dịch SIM {trade.SimNumber?.PhoneNumber} do hết thời hạn phản hồi. Bạn đã là chủ sở hữu chính thức của SIM này.",
                actionUrl: $"/trade/{trade.Id}",
                referenceType: "TradeTransaction",
                referenceId: trade.Id,
                sendEmail: true,
                cancellationToken: cancellationToken);
        }
    }
}
