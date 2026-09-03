using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý hồ sơ xác minh KYC (IdentityVerification).
/// </summary>
public interface IIdentityVerificationRepository
{
    Task<IdentityVerification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IdentityVerification?> GetLatestByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<IdentityVerification> Items, int TotalCount)> GetPagedAsync(
        VerificationStatus? status,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task AddAsync(IdentityVerification verification, CancellationToken cancellationToken = default);
    void Update(IdentityVerification verification);
}
