using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

public class SimCategoryRepository : ISimCategoryRepository
{
    private readonly TrustSimDbContext _context;

    public SimCategoryRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SimCategory>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SimCategories
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.CategoryName)
            .ToListAsync(cancellationToken);
    }

    public async Task<SimCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SimCategories
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);
    }

    public async Task<SimCategory?> GetByCodeAsync(string categoryCode, CancellationToken cancellationToken = default)
    {
        return await _context.SimCategories
            .FirstOrDefaultAsync(c => EF.Functions.ILike(c.CategoryCode, categoryCode) && !c.IsDeleted, cancellationToken);
    }

    public async Task<bool> ExistsByCodeAsync(string categoryCode, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.SimCategories.Where(c => !c.IsDeleted);
        if (excludeId.HasValue)
        {
            query = query.Where(c => c.Id != excludeId.Value);
        }
        return await query.AnyAsync(c => EF.Functions.ILike(c.CategoryCode, categoryCode), cancellationToken);
    }

    public async Task AddAsync(SimCategory category, CancellationToken cancellationToken = default)
    {
        await _context.SimCategories.AddAsync(category, cancellationToken);
    }

    public void Update(SimCategory category)
    {
        _context.SimCategories.Update(category);
    }
}
