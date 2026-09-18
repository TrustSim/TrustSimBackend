using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

/// <summary>
/// Triển khai ISimCategoryMappingRepository bằng EF Core trên TrustSimDbContext.
/// </summary>
public class SimCategoryMappingRepository : ISimCategoryMappingRepository
{
    private readonly TrustSimDbContext _context;

    public SimCategoryMappingRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SimCategoryMapping>> GetBySimNumberIdAsync(Guid simNumberId, CancellationToken cancellationToken = default)
    {
        return await _context.SimCategoryMappings
            .Include(m => m.SimCategory)
            .Where(m => m.SimNumberId == simNumberId && m.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<SimCategoryMapping> mappings, CancellationToken cancellationToken = default)
    {
        await _context.SimCategoryMappings.AddRangeAsync(mappings, cancellationToken);
    }

    public void RemoveRange(IEnumerable<SimCategoryMapping> mappings)
    {
        _context.SimCategoryMappings.RemoveRange(mappings);
    }
}
