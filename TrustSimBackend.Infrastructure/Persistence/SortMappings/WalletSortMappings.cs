using System.Linq.Expressions;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Infrastructure.Persistence.SortMappings;

/// <summary>
/// Cấu hình Whitelist Mapping sắp xếp cho các truy vấn Tài chính & Ví (Wallet, WalletTransaction, LedgerJournal).
/// </summary>
public static class WalletSortMappings
{
    /// <summary>
    /// Sắp xếp cho danh sách Ví thành viên (Wallet).
    /// </summary>
    public static readonly IReadOnlyDictionary<WalletSortBy, Expression<Func<Wallet, object>>> WalletMappings =
        new Dictionary<WalletSortBy, Expression<Func<Wallet, object>>>
        {
            [WalletSortBy.CreatedDate] = w => w.CreatedDate,
            [WalletSortBy.AvailableBalance] = w => w.AvailableBalance,
            [WalletSortBy.LockedBalance] = w => w.LockedBalance,
            [WalletSortBy.Status] = w => w.WalletStatus,
            [WalletSortBy.UserName] = w => w.UserAccount != null && w.UserAccount.UserName != null ? w.UserAccount.UserName : string.Empty
        };

    /// <summary>
    /// Sắp xếp cho lịch sử Biến động số dư ví (WalletTransaction).
    /// </summary>
    public static readonly IReadOnlyDictionary<WalletTransactionSortBy, Expression<Func<WalletTransaction, object>>> TransactionMappings =
        new Dictionary<WalletTransactionSortBy, Expression<Func<WalletTransaction, object>>>
        {
            [WalletTransactionSortBy.CreatedAt] = t => t.CreatedAt,
            [WalletTransactionSortBy.Amount] = t => t.Amount,
            [WalletTransactionSortBy.Status] = t => t.TransactionStatus,
            [WalletTransactionSortBy.TransactionType] = t => t.WalletTransactionType,
            [WalletTransactionSortBy.TransactionCode] = t => t.TransactionCode
        };

    /// <summary>
    /// Sắp xếp cho Sổ cái kiểm toán kép (LedgerJournal).
    /// </summary>
    public static readonly IReadOnlyDictionary<LedgerJournalSortBy, Expression<Func<LedgerJournal, object>>> LedgerJournalMappings =
        new Dictionary<LedgerJournalSortBy, Expression<Func<LedgerJournal, object>>>
        {
            [LedgerJournalSortBy.PostedAt] = j => j.PostedAt,
            [LedgerJournalSortBy.CreatedDate] = j => j.CreatedDate,
            [LedgerJournalSortBy.JournalCode] = j => j.JournalCode,
            [LedgerJournalSortBy.TransactionType] = j => j.TransactionType
        };
}
