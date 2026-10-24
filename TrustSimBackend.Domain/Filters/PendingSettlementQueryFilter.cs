using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Filters;

/// <summary>
/// Bộ lọc danh sách các phiên đấu giá đang cần người dùng thanh toán hoặc xử lý quyết định.
/// </summary>
public class PendingSettlementQueryFilter : BaseQueryFilter<PendingSettlementSortBy>
{
    /// <summary>
    /// Vai trò của người dùng trong phiên cần xử lý: WINNER, RUNNER_UP, SELLER.
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// Hành động nghiệp vụ cần thực hiện: PAY_SETTLEMENT, RESPOND_RUNNER_UP_OFFER, DECIDE_RUNNER_UP.
    /// </summary>
    public string? ActionRequired { get; set; }
}
