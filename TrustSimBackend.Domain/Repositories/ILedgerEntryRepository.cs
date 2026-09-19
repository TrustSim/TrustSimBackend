using TrustSimBackend.Domain.Entities.Financial;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý truy xuất các mắt xích sổ cái bất biến (LedgerEntry).
/// </summary>
public interface ILedgerEntryRepository
{
    Task<LedgerEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LedgerEntry?> GetLastEntryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LedgerEntry>> GetByJournalIdAsync(Guid journalId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<LedgerEntry> Items, int TotalCount)> GetByWalletIdPagedAsync(
        Guid walletId,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LedgerEntry>> GetAllOrderedBySequenceAsync(
        long fromSequence = 1,
        int limit = 10000,
        CancellationToken cancellationToken = default);
    Task<long> GetTotalCountAsync(CancellationToken cancellationToken = default);
    Task AddAsync(LedgerEntry entry, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<LedgerEntry> entries, CancellationToken cancellationToken = default);
}
