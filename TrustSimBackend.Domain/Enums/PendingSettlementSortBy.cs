namespace TrustSimBackend.Domain.Enums;

/// <summary>
/// Các trường sắp xếp cho danh sách giao dịch đấu giá đang chờ xử lý quyết định / thanh toán (Pending Settlements).
/// </summary>
public enum PendingSettlementSortBy
{
    Deadline = 1,
    RemainingPaymentRequired = 2,
    FinalPrice = 3
}
