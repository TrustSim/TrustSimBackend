using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý truy xuất chứng từ kế toán ghi kép (LedgerJournal).
/// </summary>
public interface ILedgerJournalRepository
{
    Task<LedgerJournal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LedgerJournal?> GetByCodeAsync(string journalCode, CancellationToken cancellationToken = default);
    Task<LedgerJournal?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<LedgerJournal> Items, int TotalCount)> GetPagedAsync(
        LedgerTransactionType? transactionType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
    Task AddAsync(LedgerJournal journal, CancellationToken cancellationToken = default);
}
