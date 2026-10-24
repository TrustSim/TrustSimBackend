using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Ai;

/// <summary>
/// Đại diện cho một tin nhắn trong phiên tư vấn AI.
/// Lưu nội dung hội thoại, vai trò người gửi, các SIM trong kho TrustSim được đề xuất và tài liệu RAG đã trích xuất.
/// </summary>
public class AiConsultationMessage : FullAuditableEntity
{
    /// <summary>ID của phiên tư vấn (<see cref="AiConsultationSession"/>).</summary>
    public Guid SessionId { get; set; }

    /// <summary>Vai trò người gửi: USER, ASSISTANT, SYSTEM.</summary>
    public ChatSenderRole SenderRole { get; set; } = ChatSenderRole.USER;

    /// <summary>Nội dung văn bản của tin nhắn.</summary>
    public string MessageContent { get; set; } = string.Empty;

    /// <summary>Danh sách các số SIM trong kho TrustSim được AI đề xuất (lưu dưới dạng JSON Array các SimNumber IDs và lý do).</summary>
    public string? RecommendedSimIds { get; set; } 

    /// <summary>Danh sách các DocumentChunk IDs được trích xuất ngữ cảnh RAG (JSON Array).</summary>
    public string? ReferencedChunkIds { get; set; }

    /// <summary>Số lượng token tiêu tốn cho tin nhắn này.</summary>
    public int TokensUsed { get; set; } = 0;

    // --- Navigation Property ---
    /// <summary>Phiên tư vấn chứa tin nhắn này.</summary>
    public virtual AiConsultationSession? Session { get; set; }
}
