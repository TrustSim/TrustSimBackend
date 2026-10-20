using System;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Filters;

/// <summary>
/// Bộ lọc tìm kiếm, phân trang và sắp xếp cho danh sách phiên đấu giá.
/// </summary>
public class AuctionQueryFilter : BaseQueryFilter<AuctionSortBy>
{
    public AuctionStatus? Status { get; set; }
    public AuctionType? Type { get; set; }
    public Guid? CarrierId { get; set; }
    public Guid? CategoryId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? Role { get; set; } // "author" | "participant"
    public Guid? AuthorId { get; set; }
    public Guid? ParticipantId { get; set; }
}
