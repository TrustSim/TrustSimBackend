using TrustSimBackend.Domain.Entities.Sim;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý lịch sử xác minh SIM (SimVerification).
/// </summary>
public interface ISimVerificationRepository
{
    Task<SimVerification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SimVerification>> GetByOwnershipIdAsync(Guid simOwnershipId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SimVerification>> GetBySimNumberIdAsync(Guid simNumberId, CancellationToken cancellationToken = default);
    Task AddAsync(SimVerification verification, CancellationToken cancellationToken = default);
    void Update(SimVerification verification);
}
