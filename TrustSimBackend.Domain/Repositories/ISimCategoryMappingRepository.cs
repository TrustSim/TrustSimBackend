using TrustSimBackend.Domain.Entities.Sim;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý ánh xạ thể loại/tổ hợp số SIM (SimCategoryMapping).
/// </summary>
public interface ISimCategoryMappingRepository
{
    Task<IReadOnlyList<SimCategoryMapping>> GetBySimNumberIdAsync(Guid simNumberId, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<SimCategoryMapping> mappings, CancellationToken cancellationToken = default);
    void RemoveRange(IEnumerable<SimCategoryMapping> mappings);
}
