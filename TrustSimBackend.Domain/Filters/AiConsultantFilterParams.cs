using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Filters;

/// <summary>
/// Bộ lọc danh sách các phiên tư vấn AI của người dùng.
/// </summary>
public class ConsultationSessionQueryFilter : BaseQueryFilter<ConsultationSessionSortBy>
{
}

/// <summary>
/// Bộ lọc danh sách tin nhắn trong phiên tư vấn AI.
/// </summary>
public class ConsultationMessageQueryFilter : BaseQueryFilter<ConsultationMessageSortBy>
{
    /// <summary>
    /// Lọc theo vai trò người gửi (USER hoặc ASSISTANT).
    /// </summary>
    public ChatSenderRole? SenderRole { get; set; }
}
