using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

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
        return await _context.LedgerJournals
            .Include(j => j.Entries.OrderBy(e => e.SequenceNumber))
            .FirstOrDefaultAsync(j => j.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task<(IReadOnlyList<LedgerJournal> Items, int TotalCount)> GetPagedAsync(
        LedgerTransactionType? transactionType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _context.LedgerJournals
            .Include(j => j.Entries.OrderBy(e => e.SequenceNumber))
            .AsNoTracking();

        if (transactionType.HasValue)
        {
            query = query.Where(j => j.TransactionType == transactionType.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(j => j.PostedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(j => j.PostedAt <= toDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(j => j.PostedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(LedgerJournal journal, CancellationToken cancellationToken = default)
    {
        await _context.LedgerJournals.AddAsync(journal, cancellationToken);
    }
}
