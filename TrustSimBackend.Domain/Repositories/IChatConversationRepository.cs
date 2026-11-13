using TrustSimBackend.Domain.Entities.Chat;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý truy xuất và lưu trữ các cuộc hội thoại Chat (Direct &amp; Support).
/// </summary>
public interface IChatConversationRepository
{
    Task<ChatConversation?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ChatConversation?> GetDirectConversationBetweenUsersAsync(Guid userAId, Guid userBId, CancellationToken cancellationToken = default);
    Task<ChatConversation?> GetActiveSupportConversationByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ChatConversation?> GetPendingSupportConversationByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatConversation>> GetClaimedSupportConversationsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<ChatConversation> Items, int TotalCount)> GetUserConversationsPagedAsync(
        Guid currentUserId,
        UserRole currentUserRole,
        ConversationType? typeFilter,
        SupportConversationStatus? supportStatusFilter,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<bool> TryClaimSupportConversationAtomicallyAsync(
        Guid conversationId,
        Guid staffUserId,
        DateTime claimedAt,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserAccount>> SearchVerifiedUsersByEmailOrPhoneAsync(
        string keyword,
        Guid excludeUserId,
        int limit = 10,
        CancellationToken cancellationToken = default);
    Task AddAsync(ChatConversation conversation, CancellationToken cancellationToken = default);
    void Update(ChatConversation conversation);
}
