using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

/// <summary>
/// Triển khai ILedgerEntryRepository sử dụng EF Core.
/// </summary>
public class LedgerEntryRepository : ILedgerEntryRepository
{
    private readonly TrustSimDbContext _context;

    public LedgerEntryRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<LedgerEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.LedgerEntries
            .Include(e => e.Journal)
            .Include(e => e.Wallet)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<LedgerEntry?> GetLastEntryAsync(CancellationToken cancellationToken = default)
    {
        var localMax = _context.LedgerEntries.Local
            .OrderByDescending(e => e.SequenceNumber)
            .FirstOrDefault();

        var dbMax = await _context.LedgerEntries
            .OrderByDescending(e => e.SequenceNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (localMax == null) return dbMax;
        if (dbMax == null) return localMax;

        return localMax.SequenceNumber >= dbMax.SequenceNumber ? localMax : dbMax;
    }

    public async Task<IReadOnlyList<LedgerEntry>> GetByJournalIdAsync(Guid journalId, CancellationToken cancellationToken = default)
    {
        return await _context.LedgerEntries
            .Where(e => e.JournalId == journalId)
            .OrderBy(e => e.SequenceNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<LedgerEntry> Items, int TotalCount)> GetByWalletIdPagedAsync(
        Guid walletId,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _context.LedgerEntries
            .Where(e => e.WalletId == walletId)
            .AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(e => e.SequenceNumber)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<LedgerEntry>> GetAllOrderedBySequenceAsync(
        long fromSequence = 1,
        int limit = 10000,
        CancellationToken cancellationToken = default)
    {
        return await _context.LedgerEntries
            .Where(e => e.SequenceNumber >= fromSequence)
            .OrderBy(e => e.SequenceNumber)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<long> GetTotalCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.LedgerEntries.LongCountAsync(cancellationToken);
    }

    public async Task AddAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
    {
        await _context.LedgerEntries.AddAsync(entry, cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<LedgerEntry> entries, CancellationToken cancellationToken = default)
    {
        await _context.LedgerEntries.AddRangeAsync(entries, cancellationToken);
    }
}
