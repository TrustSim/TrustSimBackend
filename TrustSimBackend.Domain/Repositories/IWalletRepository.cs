using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý truy xuất và cập nhật thực thể Wallet.
/// </summary>
public interface IWalletRepository
{
    Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Wallet> Items, int TotalCount)> GetPagedAsync(
        AdminWalletQueryFilter filter,
        CancellationToken cancellationToken = default);
    Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default);
    void Update(Wallet wallet);
}
