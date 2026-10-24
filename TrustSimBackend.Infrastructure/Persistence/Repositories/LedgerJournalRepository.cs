using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence.Extensions;
using TrustSimBackend.Infrastructure.Persistence.SortMappings;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

/// <summary>
/// Triển khai ILedgerJournalRepository sử dụng EF Core.
/// </summary>
public class LedgerJournalRepository : ILedgerJournalRepository
{
    private readonly TrustSimDbContext _context;

    public LedgerJournalRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<LedgerJournal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.LedgerJournals
            .Include(j => j.Entries.OrderBy(e => e.SequenceNumber))
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
    }

    public async Task<LedgerJournal?> GetByCodeAsync(string journalCode, CancellationToken cancellationToken = default)
    {
        return await _context.LedgerJournals
            .Include(j => j.Entries.OrderBy(e => e.SequenceNumber))
            .FirstOrDefaultAsync(j => j.JournalCode == journalCode, cancellationToken);
    }

    public async Task<LedgerJournal?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var local = _context.LedgerJournals.Local
            .FirstOrDefault(j => j.IdempotencyKey == idempotencyKey);
        if (local != null) return local;

        return await _context.LedgerJournals
            .Include(j => j.Entries.OrderBy(e => e.SequenceNumber))
            .FirstOrDefaultAsync(j => j.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task<(IReadOnlyList<LedgerJournal> Items, int TotalCount)> GetPagedAsync(
        LedgerJournalQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.LedgerJournals
            .Include(j => j.Entries.OrderBy(e => e.SequenceNumber))
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var clean = filter.Keyword.Trim();
            var isGuid = Guid.TryParse(clean, out var cleanGuid);

            query = query.Where(j => j.JournalCode.Contains(clean)
                || (j.Description != null && j.Description.Contains(clean))
                || (j.ReferenceType != null && j.ReferenceType.Contains(clean))
                || (isGuid && j.ReferenceId == cleanGuid));
        }

        if (filter.TransactionType.HasValue)
        {
            query = query.Where(j => j.TransactionType == filter.TransactionType.Value);
        }

        if (filter.TransactionTypes != null && filter.TransactionTypes.Any())
        {
            query = query.Where(j => filter.TransactionTypes.Contains(j.TransactionType));
        }

        if (filter.MinAmount.HasValue)
        {
            query = query.Where(j => j.Entries.Any(e => e.Amount >= filter.MinAmount.Value));
        }

        if (filter.MaxAmount.HasValue)
        {
            query = query.Where(j => j.Entries.Any(e => e.Amount <= filter.MaxAmount.Value));
        }

        query = query.ApplyDateRange(j => j.PostedAt, filter.FromDate, filter.ToDate);
        query = query.ApplySafeSorting(WalletSortMappings.LedgerJournalMappings, filter.SortBy, filter.SortOrder, defaultSortExpression: j => j.PostedAt, defaultSortOrder: SortOrder.Desc);

        return await query.ToPagedTupleAsync(filter.PageIndex, filter.PageSize, cancellationToken);
    }

    public async Task AddAsync(LedgerJournal journal, CancellationToken cancellationToken = default)
    {
        await _context.LedgerJournals.AddAsync(journal, cancellationToken);
    }
}
