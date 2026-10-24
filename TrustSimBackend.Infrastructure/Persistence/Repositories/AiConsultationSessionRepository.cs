using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Ai;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence.Extensions;
using TrustSimBackend.Infrastructure.Persistence.SortMappings;

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

    public async Task<(IReadOnlyList<AiConsultationSession> Items, int TotalCount)> GetPagedSessionsByUserIdAsync(
        Guid userId,
        ConsultationSessionQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AiConsultationSessions
            .Include(s => s.Messages)
            .AsNoTracking()
            .Where(s => s.UserAccountId == userId && s.IsActive);

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var clean = filter.Keyword.Trim().ToLower();
            query = query.Where(s => s.SessionTitle.ToLower().Contains(clean));
        }

        query = query.ApplyDateRange(s => s.CreatedDate, filter.FromDate, filter.ToDate);
        query = query.ApplySafeSorting(AiConsultantSortMappings.SessionMappings, filter.SortBy, filter.SortOrder, defaultSortExpression: s => s.CreatedDate, defaultSortOrder: SortOrder.Desc);

        return await query.ToPagedTupleAsync(filter.PageIndex, filter.PageSize, cancellationToken);
    }

    public async Task<(IReadOnlyList<AiConsultationMessage> Items, int TotalCount)> GetPagedMessagesAsync(
        Guid sessionId,
        ConsultationMessageQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AiConsultationMessages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId && m.IsActive);

        if (filter.SenderRole.HasValue)
        {
            query = query.Where(m => m.SenderRole == filter.SenderRole.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var clean = filter.Keyword.Trim().ToLower();
            query = query.Where(m => m.MessageContent.ToLower().Contains(clean));
        }

        query = query.ApplyDateRange(m => m.CreatedDate, filter.FromDate, filter.ToDate);
        query = query.ApplySafeSorting(AiConsultantSortMappings.MessageMappings, filter.SortBy, filter.SortOrder, defaultSortExpression: m => m.CreatedDate, defaultSortOrder: SortOrder.Asc);

        return await query.ToPagedTupleAsync(filter.PageIndex, filter.PageSize, cancellationToken);
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
