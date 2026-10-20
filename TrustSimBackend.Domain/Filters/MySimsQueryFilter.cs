using System;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Filters;

/// <summary>
/// Bộ lọc danh sách kho SIM thuộc sở hữu của người dùng hiện tại (Người bán).
/// </summary>
public class MySimsQueryFilter : BaseQueryFilter<SimOwnershipSortBy>
{
    public SimStatus? Status { get; set; }
    public Guid? CarrierId { get; set; }
    public Guid? CategoryId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
}
