using System;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Filters;

/// <summary>
/// Bộ lọc danh sách toàn bộ kho SIM hệ thống dành cho Quản trị viên.
/// </summary>
public class AdminSimsQueryFilter : BaseQueryFilter<SimSortBy>
{
    public SimStatus? Status { get; set; }
    public Guid? CarrierId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? PrefixId { get; set; }
    public Guid? FormatRhythmId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
}
