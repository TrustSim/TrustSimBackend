using TrustSimBackend.Domain.Entities.Auctions;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý lượt trả giá đấu giá (Bid).
/// </summary>
public interface IBidRepository
{
    Task<IReadOnlyList<Bid>> GetBidsByAuctionIdAsync(Guid auctionId, int top = 50, CancellationToken cancellationToken = default);
    Task<Bid?> GetHighestBidAsync(Guid auctionId, CancellationToken cancellationToken = default);
    Task AddAsync(Bid bid, CancellationToken cancellationToken = default);
    Task<int> CountByAuctionIdAsync(Guid auctionId, CancellationToken cancellationToken = default);
}
