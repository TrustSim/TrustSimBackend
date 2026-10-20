namespace TrustSimBackend.Domain.Enums;

/// <summary>
/// Các tiêu chí sắp xếp cho phiên đấu giá (Auction).
/// </summary>
public enum AuctionSortBy
{
    CreatedDate = 1,
    CurrentPrice = 2,
    StartingPrice = 3,
    EndAt = 4,
    ReservePrice = 5,
    BidCount = 6,
    Status = 7
}
