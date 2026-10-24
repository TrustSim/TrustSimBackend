using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Filters;

/// <summary>
/// Bộ lọc tra cứu lịch sử định giá của số SIM.
/// </summary>
public class SimValuationQueryFilter : BaseQueryFilter<SimValuationSortBy>
{
    /// <summary>
    /// Đã được người bán áp dụng làm giá bán chính thức hay chưa.
    /// </summary>
    public bool? AppliedToSim { get; set; }

    /// <summary>
    /// Tên model AI thực hiện định giá (GPT-4o, Claude, DeepSeek...).
    /// </summary>
    public string? AiModelName { get; set; }
}
