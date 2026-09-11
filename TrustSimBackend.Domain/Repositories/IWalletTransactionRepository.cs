using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý lịch sử giao dịch nạp / rút ví (WalletTransaction).
/// </summary>
public interface IWalletTransactionRepository
{
    Task<WalletTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WalletTransaction?> GetByTransactionCodeAsync(string transactionCode, CancellationToken cancellationToken = default);
    Task<WalletTransaction?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByTransactionCodeAsync(string transactionCode, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<WalletTransaction> Items, int TotalCount)> GetPagedByWalletIdAsync(
        Guid walletId,
        WalletTransactionType? type,
        TransactionStatus? status,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<WalletTransaction> Items, int TotalCount)> GetPagedAllAsync(
        WalletTransactionType? type,
        TransactionStatus? status,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task AddAsync(WalletTransaction transaction, CancellationToken cancellationToken = default);
    void Update(WalletTransaction transaction);
}
