using System.Linq.Expressions;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Infrastructure.Persistence.SortMappings;

public static class BidSortMappings
{
    public static readonly IReadOnlyDictionary<BidSortBy, Expression<Func<Bid, object>>> BidMappings =
        new Dictionary<BidSortBy, Expression<Func<Bid, object>>>
        {
            [BidSortBy.BidAmount] = b => b.BidAmount,
            [BidSortBy.PlacedAt] = b => b.PlacedAt
        };
}
