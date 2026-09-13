using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Ai;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

public class AiConsultationSessionRepository : IAiConsultationSessionRepository
{
    private readonly TrustSimDbContext _context;

    public AiConsultationSessionRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<AiConsultationSession?> GetByIdAsync(Guid id, bool includeMessages = true, CancellationToken cancellationToken = default)
    {
        IQueryable<AiConsultationSession> query = _context.AiConsultationSessions;

        if (includeMessages)
        {
            query = query.Include(s => s.Messages.OrderBy(m => m.CreatedDate));
        }

        return await query.FirstOrDefaultAsync(s => s.Id == id && s.IsActive, cancellationToken);
    }

    public async Task<IReadOnlyList<AiConsultationSession>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.AiConsultationSessions
            .Where(s => s.UserAccountId == userId && s.IsActive)
            .OrderByDescending(s => s.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddSessionAsync(AiConsultationSession session, CancellationToken cancellationToken = default)
    {
        await _context.AiConsultationSessions.AddAsync(session, cancellationToken);
    }

    public async Task AddMessageAsync(AiConsultationMessage message, CancellationToken cancellationToken = default)
    {
        await _context.AiConsultationMessages.AddAsync(message, cancellationToken);
    }

    public async Task<IReadOnlyList<AiConsultationMessage>> GetRecentMessagesAsync(Guid sessionId, int count = 10, CancellationToken cancellationToken = default)
    {
        var messages = await _context.AiConsultationMessages
            .Where(m => m.SessionId == sessionId && m.IsActive)
            .OrderByDescending(m => m.CreatedDate)
            .Take(count)
            .ToListAsync(cancellationToken);

        messages.Reverse(); // Sắp xếp lại theo thứ tự thời gian tăng dần
        return messages;
    }

    public void UpdateSession(AiConsultationSession session)
    {
        _context.AiConsultationSessions.Update(session);
    }
}
