using TrustSimBackend.Domain.Entities.Sim;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý quyền sở hữu SIM (SimOwnership).
/// </summary>
public interface ISimOwnershipRepository
{
    Task<SimOwnership?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SimOwnership?> GetActiveOwnershipByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);
    Task<SimOwnership?> GetActiveOwnershipBySimNumberIdAsync(Guid simNumberId, CancellationToken cancellationToken = default);
    Task<SimOwnership?> GetLatestOwnershipByUserAndSimNumberIdAsync(Guid userId, Guid simNumberId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SimOwnership>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> HasActiveOwnershipAsync(string phoneNumber, Guid? excludeUserId = null, CancellationToken cancellationToken = default);
    Task AddAsync(SimOwnership ownership, CancellationToken cancellationToken = default);
    void Update(SimOwnership ownership);
}
