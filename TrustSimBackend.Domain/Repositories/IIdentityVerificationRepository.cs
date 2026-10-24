using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý hồ sơ xác minh KYC (IdentityVerification).
/// </summary>
public interface IIdentityVerificationRepository
{
    Task<IdentityVerification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IdentityVerification?> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IdentityVerification?> GetLatestByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IdentityVerification>> GetHistoryByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<IdentityVerification> Items, int TotalCount)> GetPagedHistoryByUserIdAsync(
        Guid userId,
        UserKycHistoryQueryFilter filter,
        CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<IdentityVerification> Items, int TotalCount)> GetPagedAsync(
        AdminKycQueryFilter filter,
        CancellationToken cancellationToken = default);
    Task AddAsync(IdentityVerification verification, CancellationToken cancellationToken = default);
    void Update(IdentityVerification verification);
}
