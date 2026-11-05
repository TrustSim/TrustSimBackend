using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Chat;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence.Extensions;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

/// <summary>
/// Triển khai IChatMessageRepository sử dụng Entity Framework Core.
/// </summary>
public class ChatMessageRepository : IChatMessageRepository
{
    private readonly TrustSimDbContext _context;

    public ChatMessageRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<ChatMessage> Items, int TotalCount)> GetMessagesByConversationPagedAsync(
        Guid conversationId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ChatMessages
            .Include(m => m.Sender)
                .ThenInclude(u => u!.UserProfile)
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && !m.IsDeleted)
            .OrderByDescending(m => m.CreatedDate);

        return await query.ToPagedTupleAsync(pageIndex, pageSize, cancellationToken);
    }

    public async Task<int> MarkMessagesAsReadAsync(
        Guid conversationId,
        Guid readerUserId,
        DateTime readAt,
        CancellationToken cancellationToken = default)
    {
        if (_context.Database.IsRelational())
        {
            return await _context.ChatMessages
                .Where(m => m.ConversationId == conversationId
                         && !m.IsDeleted
                         && !m.IsRead
                         && m.SenderId != readerUserId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(m => m.IsRead, true)
                    .SetProperty(m => m.ReadAt, readAt)
                    .SetProperty(m => m.UpdatedDate, readAt),
                    cancellationToken);
        }

        // Fallback dành cho InMemory Database
        var unreadMessages = await _context.ChatMessages
            .Where(m => m.ConversationId == conversationId
                     && !m.IsDeleted
                     && !m.IsRead
                     && m.SenderId != readerUserId)
            .ToListAsync(cancellationToken);

        foreach (var msg in unreadMessages)
        {
            msg.IsRead = true;
            msg.ReadAt = readAt;
            msg.UpdatedDate = readAt;
        }

        return unreadMessages.Count;
    }

    public async Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        await _context.ChatMessages.AddAsync(message, cancellationToken);
    }
}
