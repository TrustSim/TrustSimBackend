using TrustSimBackend.Domain.Entities.Financial;

namespace TrustSimBackend.Domain.Repositories;

public interface IAuctionTransactionRepository
{
    Task<AuctionTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuctionTransaction>> GetByAuctionIdAsync(Guid auctionId, CancellationToken cancellationToken = default);
    Task AddAsync(AuctionTransaction transaction, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<AuctionTransaction> transactions, CancellationToken cancellationToken = default);
}
