namespace TrustSimBackend.Domain.Enums;

/// <summary>
/// Trạng thái xử lý của cuộc hội thoại hỗ trợ với Sàn (Support Chat).
/// </summary>
public enum SupportConversationStatus
{
    /// <summary>Không áp dụng (dành cho hội thoại DIRECT giữa 2 người dùng).</summary>
    NONE = 0,

    /// <summary>Đang chờ tiếp nhận: Mọi Nhân viên Sàn (CONSULTANT) và Admin đều thấy và có thể bấm Duyệt.</summary>
    PENDING = 1,

    /// <summary>Đã được một Nhân viên Sàn hoặc Admin duyệt nhận: Trở thành riêng tư tuyệt đối giữa người đó và khách hàng.</summary>
    CLAIMED = 2,

    /// <summary>Đã đóng / hoàn tất hỗ trợ.</summary>
    RESOLVED = 3
}
