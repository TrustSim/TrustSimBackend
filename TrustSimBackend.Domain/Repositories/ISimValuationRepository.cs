using TrustSimBackend.Domain.Entities.Ai;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý Lịch sử Định giá SIM (SimValuation)
/// </summary>
public interface ISimValuationRepository
{
    Task<SimValuation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SimValuation?> GetLatestBySimNumberIdAsync(Guid simNumberId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SimValuation>> GetHistoryBySimNumberIdAsync(Guid simNumberId, CancellationToken cancellationToken = default);
    Task AddAsync(SimValuation valuation, CancellationToken cancellationToken = default);
    void Update(SimValuation valuation);
}
