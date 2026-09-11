using TrustSimBackend.Domain.Entities.Sim;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý Thể loại SIM số đẹp (SimCategory).
/// </summary>
public interface ISimCategoryRepository
{
    Task<IReadOnlyList<SimCategory>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<SimCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SimCategory?> GetByCodeAsync(string categoryCode, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string categoryCode, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task AddAsync(SimCategory category, CancellationToken cancellationToken = default);
    void Update(SimCategory category);
}
