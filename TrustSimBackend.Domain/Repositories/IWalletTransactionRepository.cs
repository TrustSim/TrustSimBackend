using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Filters;

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
        WalletTransactionQueryFilter filter,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WalletTransaction>> GetListByWalletIdAsync(
        Guid walletId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<WalletTransaction> Items, int TotalCount)> GetPagedAllAsync(
        AdminWalletTransactionsFilter filter,
        CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<WalletTransaction> Items, int TotalCount)> GetPagedWithdrawalsAsync(
        AdminWithdrawalQueryFilter filter,
        CancellationToken cancellationToken = default);
    Task<WalletTransaction?> GetActivePendingTopUpByWalletIdAsync(Guid walletId, CancellationToken cancellationToken = default);
    Task<bool> HasActivePendingWithdrawalAsync(Guid walletId, CancellationToken cancellationToken = default);
    Task AddAsync(WalletTransaction transaction, CancellationToken cancellationToken = default);
    void Update(WalletTransaction transaction);
}

