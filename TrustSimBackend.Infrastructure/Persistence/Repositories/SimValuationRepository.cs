using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Ai;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

public class SimValuationRepository : ISimValuationRepository
{
    private readonly TrustSimDbContext _context;

    public SimValuationRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<SimValuation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SimValuations
            .Include(v => v.SimNumber)
            .Include(v => v.UserAccount)
            .FirstOrDefaultAsync(v => v.Id == id && v.IsActive, cancellationToken);
    }

    public async Task<SimValuation?> GetLatestBySimNumberIdAsync(Guid simNumberId, CancellationToken cancellationToken = default)
    {
        return await _context.SimValuations
            .Include(v => v.SimNumber)
            .Where(v => v.SimNumberId == simNumberId && v.IsActive)
            .OrderByDescending(v => v.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SimValuation>> GetHistoryBySimNumberIdAsync(Guid simNumberId, CancellationToken cancellationToken = default)
    {
        return await _context.SimValuations
            .Include(v => v.SimNumber)
            .Include(v => v.UserAccount)
            .Where(v => v.SimNumberId == simNumberId && v.IsActive)
            .OrderByDescending(v => v.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(SimValuation valuation, CancellationToken cancellationToken = default)
    {
        await _context.SimValuations.AddAsync(valuation, cancellationToken);
    }

    public void Update(SimValuation valuation)
    {
        _context.SimValuations.Update(valuation);
    }
}
