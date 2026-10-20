using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Filters;

/// <summary>
/// Bộ lọc tra cứu nhật ký kiểm toán hệ thống (Audit Logs).
/// </summary>
public class AuditLogQueryFilter : BaseQueryFilter<AuditLogSortBy>
{
    /// <summary>
    /// Tên bảng / thực thể (UserAccount, Auction, TradeTransaction...).
    /// </summary>
    public string? EntityName { get; set; }

    /// <summary>
    /// ID của bản ghi thực thể chịu tác động.
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// ID của người thực hiện hành động.
    /// </summary>
    public Guid? ActorId { get; set; }

    /// <summary>
    /// Loại hành động kiểm toán (CREATE, UPDATE, DELETE, LOGIN...).
    /// </summary>
    public ActionType? ActionType { get; set; }
}

/// <summary>
/// Bộ lọc tra cứu danh sách khiếu nại / tranh chấp giao dịch (Disputes).
/// </summary>
public class DisputeQueryFilter : BaseQueryFilter<DisputeTicketSortBy>
{
    /// <summary>
    /// Trạng thái tranh chấp (OPEN, RESOLVED, REJECTED...).
    /// </summary>
    public DisputeStatus? Status { get; set; }

    /// <summary>
    /// Lọc theo ID giao dịch chuyển nhượng SIM liên quan.
    /// </summary>
    public Guid? TradeId { get; set; }
}

/// <summary>
/// Bộ lọc tra cứu danh sách thông báo người dùng (Notifications).
/// </summary>
public class NotificationQueryFilter : BaseQueryFilter<NotificationSortBy>
{
    /// <summary>
    /// Lọc theo trạng thái đã đọc hay chưa đọc.
    /// </summary>
    public bool? IsRead { get; set; }
}
