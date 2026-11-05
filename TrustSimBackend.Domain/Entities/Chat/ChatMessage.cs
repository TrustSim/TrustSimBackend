using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Chat;

/// <summary>
/// Đại diện cho một tin nhắn trong cuộc hội thoại (Direct hoặc Support).
/// </summary>
public class ChatMessage : FullAuditableEntity
{
    /// <summary>ID của cuộc hội thoại chứa tin nhắn này.</summary>
    public Guid ConversationId { get; set; }

    /// <summary>ID tài khoản người gửi tin nhắn.</summary>
    public Guid SenderId { get; set; }

    /// <summary>Loại tin nhắn: TEXT, IMAGE, PDF_DOCUMENT, SYSTEM.</summary>
    public ChatMessageType MessageType { get; set; } = ChatMessageType.TEXT;

    /// <summary>Nội dung văn bản của tin nhắn.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>URL tệp đính kèm trên Supabase Storage (nếu có).</summary>
    public string? AttachmentUrl { get; set; }

    /// <summary>Tên tệp đính kèm gốc (nếu có).</summary>
    public string? AttachmentFileName { get; set; }

    /// <summary>Trạng thái đã đọc của người nhận.</summary>
    public bool IsRead { get; set; } = false;

    /// <summary>Thời điểm người nhận đọc tin nhắn.</summary>
    public DateTime? ReadAt { get; set; }

    // --- Navigation Properties ---
    public virtual ChatConversation? Conversation { get; set; }
    public virtual UserAccount? Sender { get; set; }
}
