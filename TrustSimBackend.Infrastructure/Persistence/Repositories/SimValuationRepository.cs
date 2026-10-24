using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Ai;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence.Extensions;
using TrustSimBackend.Infrastructure.Persistence.SortMappings;

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

    public async Task<(IReadOnlyList<SimValuation> Items, int TotalCount)> GetPagedHistoryBySimNumberIdAsync(
        Guid simNumberId,
        SimValuationQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SimValuations
            .Include(v => v.SimNumber)
            .Include(v => v.UserAccount)
            .AsNoTracking()
            .Where(v => v.SimNumberId == simNumberId && v.IsActive);

        if (filter.AppliedToSim.HasValue)
        {
            query = query.Where(v => v.AppliedToSim == filter.AppliedToSim.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.AiModelName))
        {
            var clean = filter.AiModelName.Trim().ToLower();
            query = query.Where(v => v.AiModelName.ToLower().Contains(clean));
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var clean = filter.Keyword.Trim().ToLower();
            query = query.Where(v => (v.ValuationReasoning != null && v.ValuationReasoning.ToLower().Contains(clean)) ||
                                     (v.FengShuiAnalysis != null && v.FengShuiAnalysis.ToLower().Contains(clean)));
        }

        query = query.ApplyDateRange(v => v.CreatedDate, filter.FromDate, filter.ToDate);
        query = query.ApplySafeSorting(SimValuationSortMappings.ValuationMappings, filter.SortBy, filter.SortOrder, defaultSortExpression: v => v.CreatedDate, defaultSortOrder: SortOrder.Desc);

        return await query.ToPagedTupleAsync(filter.PageIndex, filter.PageSize, cancellationToken);
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
