using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Ai;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence.Extensions;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

public class AiTelemetryRepository : IAiTelemetryRepository
{
    private readonly TrustSimDbContext _context;

    public AiTelemetryRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task AddTelemetryLogAsync(AiTelemetryLog log, CancellationToken cancellationToken = default)
    {
        await _context.AiTelemetryLogs.AddAsync(log, cancellationToken);
    }

    public async Task AddEvaluationResultAsync(AiEvaluationResult evaluation, CancellationToken cancellationToken = default)
    {
        await _context.AiEvaluationResults.AddAsync(evaluation, cancellationToken);
    }

    public async Task<IReadOnlyList<AiTelemetryLog>> GetTelemetryLogsSinceAsync(
        DateTime fromUtc,
        int maxCount = 500,
        CancellationToken cancellationToken = default)
    {
        return await _context.AiTelemetryLogs
            .Include(t => t.EvaluationResults)
            .AsNoTracking()
            .Where(t => t.IsActive && t.CreatedDate >= fromUtc)
            .OrderByDescending(t => t.CreatedDate)
            .Take(Math.Clamp(maxCount, 1, 2000))
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<AiEvaluationResult> Items, int TotalCount)> GetPagedEvaluationResultsAsync(
        string? evalType,
        bool? passed,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AiEvaluationResults
            .AsNoTracking()
            .Where(e => e.IsActive);

        if (!string.IsNullOrWhiteSpace(evalType))
        {
            var normalized = evalType.Trim().ToUpperInvariant();
            query = query.Where(e => e.EvalType.ToUpper() == normalized);
        }

        if (passed.HasValue)
        {
            query = query.Where(e => e.Passed == passed.Value);
        }

        query = query.OrderByDescending(e => e.CreatedDate);
        return await query.ToPagedTupleAsync(Math.Max(1, pageIndex), Math.Clamp(pageSize, 1, 100), cancellationToken);
    }
}
