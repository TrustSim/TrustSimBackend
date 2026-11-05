using System.ComponentModel.DataAnnotations;
using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Chat;

/// <summary>
/// Đại diện cho một cuộc hội thoại trực tuyến:
/// - DIRECT: Giữa 2 người dùng đã xác thực eKYC (InitiatorUserId và ParticipantUserId).
/// - SUPPORT: Giữa người dùng (InitiatorUserId) và Sàn TrustSim (AssignedStaffId khi đã được duyệt).
/// </summary>
public class ChatConversation : FullAuditableEntity
{
    /// <summary>Loại hội thoại: DIRECT (Người dùng - Người dùng) hoặc SUPPORT (Người dùng - Sàn).</summary>
    public ConversationType Type { get; set; }

    /// <summary>ID người dùng khởi tạo cuộc hội thoại (hoặc khách hàng cần hỗ trợ trong Support Chat).</summary>
    public Guid InitiatorUserId { get; set; }

    /// <summary>ID người dùng thứ 2 (chỉ dùng cho hội thoại DIRECT; null đối với hội thoại SUPPORT).</summary>
    public Guid? ParticipantUserId { get; set; }

    /// <summary>
    /// ID của Nhân viên Sàn (CONSULTANT) hoặc ADMIN đã bấm Duyệt/Chấp nhận hỗ trợ.
    /// Null khi SupportStatus == PENDING. Khi có giá trị, chỉ nhân viên/admin này mới được xem và nhắn tin.
    /// </summary>
    public Guid? AssignedStaffId { get; set; }

    /// <summary>Trạng thái tiếp nhận hỗ trợ (NONE cho DIRECT, PENDING hoặc CLAIMED cho SUPPORT).</summary>
    public SupportConversationStatus SupportStatus { get; set; } = SupportConversationStatus.NONE;

    /// <summary>Thời điểm Nhân viên Sàn / Admin bấm chấp nhận hỗ trợ cuộc hội thoại.</summary>
    public DateTime? ClaimedAt { get; set; }

    /// <summary>Nội dung xem trước của tin nhắn gần nhất (tối đa 500 ký tự).</summary>
    public string? LastMessagePreview { get; set; }

    /// <summary>Thời điểm gửi tin nhắn gần nhất (dùng để sắp xếp danh sách hội thoại).</summary>
    public DateTime? LastMessageAt { get; set; }

    /// <summary>ID người gửi tin nhắn gần nhất.</summary>
    public Guid? LastMessageSenderId { get; set; }

    /// <summary>Số lượng tin nhắn chưa đọc của phía InitiatorUserId.</summary>
    public int UnreadCountInitiator { get; set; }

    /// <summary>Số lượng tin nhắn chưa đọc của phía ParticipantUserId (Direct) hoặc AssignedStaffId (Support).</summary>
    public int UnreadCountTarget { get; set; }

    /// <summary>
    /// Concurrency Token chống Race Condition khi 2 Nhân viên/Admin bấm duyệt nhận cùng một lúc.
    /// </summary>
    [ConcurrencyCheck]
    public int LockVersion { get; set; } = 0;

    // --- Navigation Properties ---
    public virtual UserAccount? InitiatorUser { get; set; }
    public virtual UserAccount? ParticipantUser { get; set; }
    public virtual UserAccount? AssignedStaff { get; set; }
    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();

    /// <summary>
    /// Kiểm tra quyền truy cập nghiêm ngặt đối với cuộc hội thoại này.
    /// Đảm bảo quy tắc: Khi Support Chat đã được duyệt (CLAIMED), mọi Staff/Admin khác đều bị chặn tuyệt đối.
    /// </summary>
    public bool CanUserAccess(Guid userId, UserRole userRole)
    {
        if (Type == ConversationType.DIRECT)
        {
            return InitiatorUserId == userId || ParticipantUserId == userId;
        }

        // Loại SUPPORT:
        // 1. Chính khách hàng khởi tạo luôn có quyền truy cập
        if (InitiatorUserId == userId)
        {
            return true;
        }

        var isStaffOrAdmin = userRole == UserRole.ADMIN || userRole == UserRole.CONSULTANT;
        if (!isStaffOrAdmin)
        {
            return false;
        }

        // 2. Nếu đang ở trạng thái chờ (PENDING) và chưa ai nhận: Mọi Staff/Admin đều nhìn thấy được
        if (SupportStatus == SupportConversationStatus.PENDING && AssignedStaffId == null)
        {
            return true;
        }

        // 3. Nếu đã được duyệt (CLAIMED / RESOLVED): CHỈ DUY NHẤT nhân viên/admin đã duyệt mới được thấy & can thiệp!
        return AssignedStaffId.HasValue && AssignedStaffId.Value == userId;
    }
}
