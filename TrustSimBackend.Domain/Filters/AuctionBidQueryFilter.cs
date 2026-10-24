using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Filters;

/// <summary>
/// Bộ lọc tra cứu lịch sử và danh sách lượt trả giá đấu giá (Bid).
/// </summary>
public class AuctionBidQueryFilter : BaseQueryFilter<BidSortBy>
{
    /// <summary>
    /// Trạng thái của bid (VALID, OUTBID, CANCELLED...).
    /// </summary>
    public BidStatus? Status { get; set; }

    /// <summary>
    /// Mức giá bid tối thiểu (VNĐ).
    /// </summary>
    public decimal? MinBidAmount { get; set; }

    /// <summary>
    /// Mức giá bid tối đa (VNĐ).
    /// </summary>
    public decimal? MaxBidAmount { get; set; }
}
