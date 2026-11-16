using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Infrastructure.Persistence;

namespace TrustSimBackend.Infrastructure.Workers.Auction.DelayJobs;

/// <summary>
/// Helper dùng chung để giải phóng hoặc tịch thu tiền cọc niêm yết của Người bán và quy đổi ân hạn thanh toán.
/// </summary>
public sealed class SellerListingDepositHelper
{
    private readonly ILogger<SellerListingDepositHelper> _logger;

    public SellerListingDepositHelper(ILogger<SellerListingDepositHelper> logger)
    {
        _logger = logger;
    }

    public static int ResolvePaymentGraceMinutes(AuctionRule? rule)
    {
        if (rule?.PaymentGracePeriodMinutes is > 0)
        {
            return rule.PaymentGracePeriodMinutes;
        }

        return rule?.PaymentGracePeriodHours > 0 ? rule.PaymentGracePeriodHours * 60 : 1440;
    }

    /// <summary>
    /// Kiểm tra ví Người bán có thực sự đang giữ khoản cọc cam kết bán hợp lệ cho phiên đấu giá trước khi giải phóng hoặc tịch thu.
    /// </summary>
    public async Task<bool> CanSettleSellerListingDepositAsync(
        TrustSimDbContext context,
        Wallet? sellerWallet,
        Guid auctionId,
        decimal sellerDeposit,
        string targetIdempotencyKey,
        CancellationToken cancellationToken)
    {
        if (sellerDeposit <= 0 || sellerWallet == null)
        {
            return false;
        }

        if (sellerWallet.LockedBalance < sellerDeposit)
        {
            _logger.LogWarning(
                "[SellerDepositHelper] Bỏ qua xử lý cọc cam kết bán cho phiên {AuctionId}: Ví Seller {WalletId} chỉ còn {LockedBalance:N0} VNĐ phong tỏa (< {DepositAmount:N0} VNĐ).",
                auctionId, sellerWallet.Id, sellerWallet.LockedBalance, sellerDeposit);
            return false;
        }

        var hasLockRecord = await context.LedgerJournals
            .AnyAsync(j => j.IdempotencyKey == $"SELLER-LOCK-{auctionId}", cancellationToken)
            || await context.AuctionTransactions
            .AnyAsync(t => t.AuctionId == auctionId && t.WalletId == sellerWallet.Id && t.AuctionTransactionType == AuctionTransactionType.AUCTION_DEPOSIT_LOCK, cancellationToken);

        if (!hasLockRecord)
        {
            _logger.LogWarning(
                "[SellerDepositHelper] Bỏ qua xử lý cọc cam kết bán cho phiên {AuctionId}: Không tìm thấy chứng từ khóa cọc ban đầu của Seller {WalletId}.",
                auctionId, sellerWallet.Id);
            return false;
        }

        var alreadySettled = await context.LedgerJournals
            .AnyAsync(j => j.IdempotencyKey == targetIdempotencyKey || j.IdempotencyKey == $"SELLER-REL-{auctionId}", cancellationToken)
            || await context.AuctionTransactions
            .AnyAsync(t => t.AuctionId == auctionId
                && t.WalletId == sellerWallet.Id
                && (t.AuctionTransactionType == AuctionTransactionType.AUCTION_DEPOSIT_RELEASED
                    || t.AuctionTransactionType == AuctionTransactionType.AUCTION_DEPOSIT_FORFEITED), cancellationToken);

        if (alreadySettled)
        {
            _logger.LogInformation(
                "[SellerDepositHelper] Chứng từ cọc cam kết bán phiên {AuctionId} đã được xử lý trước đó ({Key}), bỏ qua xử lý trùng.",
                auctionId, targetIdempotencyKey);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Hoàn trả cọc cam kết bán cho Người bán một cách an toàn, ngăn ngừa làm âm số dư phong tỏa.
    /// </summary>
    public async Task TryReleaseSellerListingDepositAsync(
        TrustSimDbContext context,
        ILedgerService ledgerService,
        Wallet? sellerWallet,
        Guid auctionId,
        decimal sellerDeposit,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!await CanSettleSellerListingDepositAsync(context, sellerWallet, auctionId, sellerDeposit, $"SELLER-REL-{auctionId}", cancellationToken) || sellerWallet == null)
        {
            return;
        }

        var relJournal = await ledgerService.PostSellerListingDepositReleaseAsync(
            sellerWallet.Id,
            sellerDeposit,
            auctionId,
            cancellationToken);

        var releaseTx = new AuctionTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = sellerWallet.Id,
            AuctionId = auctionId,
            AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_RELEASED,
            TransactionStatus = TransactionStatus.COMPLETED,
            Amount = sellerDeposit,
            BalanceBefore = sellerWallet.AvailableBalance - sellerDeposit,
            BalanceAfter = sellerWallet.AvailableBalance,
            LedgerJournalId = relJournal.Id,
            JournalCode = relJournal.JournalCode,
            CreatedAt = now,
            CompletedAt = now
        };
        await context.AuctionTransactions.AddAsync(releaseTx, cancellationToken);
    }

    /// <summary>
    /// Tịch thu cọc cam kết bán của Người bán và bồi thường 50% cho Người mua một cách an toàn.
    /// </summary>
    public async Task TryForfeitSellerListingDepositAsync(
        TrustSimDbContext context,
        ILedgerService ledgerService,
        Wallet? sellerWallet,
        Wallet? buyerWallet,
        Guid auctionId,
        Guid tradeId,
        decimal sellerDeposit,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (buyerWallet == null || !await CanSettleSellerListingDepositAsync(context, sellerWallet, auctionId, sellerDeposit, $"SELLER-FORFEIT-{tradeId}", cancellationToken) || sellerWallet == null)
        {
            return;
        }

        var forfeitJournal = await ledgerService.PostSellerListingDepositForfeitedWithCompensationAsync(
            sellerWallet.Id,
            buyerWallet.Id,
            sellerDeposit,
            tradeId,
            cancellationToken);

        var compensationAmount = Math.Round(sellerDeposit * 0.5m, 2);

        var sellerForfeitTx = new AuctionTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = sellerWallet.Id,
            AuctionId = auctionId,
            AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_FORFEITED,
            TransactionStatus = TransactionStatus.COMPLETED,
            Amount = sellerDeposit,
            BalanceBefore = sellerWallet.AvailableBalance,
            BalanceAfter = sellerWallet.AvailableBalance,
            LedgerJournalId = forfeitJournal.Id,
            JournalCode = forfeitJournal.JournalCode,
            CreatedAt = now,
            CompletedAt = now
        };
        await context.AuctionTransactions.AddAsync(sellerForfeitTx, cancellationToken);

        if (compensationAmount > 0)
        {
            var buyerCompTx = new AuctionTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = buyerWallet.Id,
                AuctionId = auctionId,
                AuctionTransactionType = AuctionTransactionType.AUCTION_COMPENSATION,
                TransactionStatus = TransactionStatus.COMPLETED,
                Amount = compensationAmount,
                BalanceBefore = buyerWallet.AvailableBalance - compensationAmount,
                BalanceAfter = buyerWallet.AvailableBalance,
                LedgerJournalId = forfeitJournal.Id,
                JournalCode = forfeitJournal.JournalCode,
                CreatedAt = now,
                CompletedAt = now
            };
            await context.AuctionTransactions.AddAsync(buyerCompTx, cancellationToken);
        }
    }
}
