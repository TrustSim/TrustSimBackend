using TrustSimBackend.Domain.Entities.Sim;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý danh mục Nhà mạng viễn thông (SimCarrier).
/// </summary>
public interface ISimCarrierRepository
{
    Task<IReadOnlyList<SimCarrier>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<SimCarrier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SimCarrier?> GetByCodeAsync(string carrierCode, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string carrierCode, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task AddAsync(SimCarrier carrier, CancellationToken cancellationToken = default);
    void Update(SimCarrier carrier);
}
