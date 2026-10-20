using System;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Filters;

/// <summary>
/// Bộ lọc danh sách SIM số đẹp công khai trên sàn (lọc theo nhà mạng, danh mục, đầu số, nhịp số, khoảng giá).
/// </summary>
public class SimPublicQueryFilter : BaseQueryFilter<SimSortBy>
{
    public Guid? CarrierId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? PrefixId { get; set; }
    public Guid? FormatRhythmId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public SimStatus? Status { get; set; } = SimStatus.AVAILABLE;
}
