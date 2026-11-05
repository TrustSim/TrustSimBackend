using TrustSimBackend.Domain.Entities.Chat;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý truy xuất và lưu trữ tin nhắn ChatMessage.
/// </summary>
public interface IChatMessageRepository
{
    Task<(IReadOnlyList<ChatMessage> Items, int TotalCount)> GetMessagesByConversationPagedAsync(
        Guid conversationId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<int> MarkMessagesAsReadAsync(
        Guid conversationId,
        Guid readerUserId,
        DateTime readAt,
        CancellationToken cancellationToken = default);
    Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default);
}
