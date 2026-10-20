using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý truy xuất và lưu trữ thực thể UserAccount.
/// </summary>
public interface IUserAccountRepository
{
    Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserAccount?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);
    Task<UserAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<UserAccount?> GetByUserNameOrEmailAsync(string identifier, CancellationToken cancellationToken = default);
    Task<bool> ExistsByUserNameAsync(string userName, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsByIdentityNumberAsync(string identityNumber, Guid? excludeUserId = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsByPhoneNumberAsync(string phoneNumber, Guid? excludeUserId = null, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<UserAccount> Items, int TotalCount)> GetPagedAsync(
        AdminUsersQueryFilter filter,
        CancellationToken cancellationToken = default);
    Task AddAsync(UserAccount userAccount, CancellationToken cancellationToken = default);
    void Update(UserAccount userAccount);
}
