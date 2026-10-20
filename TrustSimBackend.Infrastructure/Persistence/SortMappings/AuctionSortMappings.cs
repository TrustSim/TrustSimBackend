using System.Linq.Expressions;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Infrastructure.Persistence.SortMappings;

/// <summary>
/// Cấu hình Whitelist Mapping sắp xếp cho các truy vấn Đấu giá (Auction).
/// </summary>
public static class AuctionSortMappings
{
    public static readonly IReadOnlyDictionary<AuctionSortBy, Expression<Func<Auction, object>>> Mappings =
        new Dictionary<AuctionSortBy, Expression<Func<Auction, object>>>
        {
            [AuctionSortBy.CreatedDate] = a => a.CreatedDate,
            [AuctionSortBy.CurrentPrice] = a => a.CurrentPrice,
            [AuctionSortBy.StartingPrice] = a => a.StartingPrice,
            [AuctionSortBy.EndAt] = a => a.EndAt,
            [AuctionSortBy.ReservePrice] = a => a.ReservePrice,
            [AuctionSortBy.BidCount] = a => a.Bids.Count,
            [AuctionSortBy.Status] = a => a.AuctionStatus
        };
}
