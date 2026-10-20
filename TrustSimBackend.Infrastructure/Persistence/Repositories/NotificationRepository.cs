using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.System;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence.Extensions;
using TrustSimBackend.Infrastructure.Persistence.SortMappings;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly TrustSimDbContext _context;

    public NotificationRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<Notification> Items, int TotalCount)> GetPagedByUserIdAsync(
        Guid userId,
        NotificationQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId && !n.IsDeleted);

        if (filter.IsRead.HasValue)
        {
            query = query.Where(n => n.IsRead == filter.IsRead.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.Trim().ToLower();
            query = query.Where(n =>
                n.Title.ToLower().Contains(kw) ||
                n.Content.ToLower().Contains(kw));
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(n => n.CreatedDate >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(n => n.CreatedDate <= filter.ToDate.Value);
        }

        query = query.ApplySafeSorting(SystemSortMappings.NotificationMappings, filter.SortBy, filter.SortOrder, defaultSortExpression: n => n.CreatedDate, defaultSortOrder: SortOrder.Desc);
        return await query.ToPagedTupleAsync(filter.PageIndex, filter.PageSize, cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead && !n.IsDeleted, cancellationToken);
    }

    public async Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        await _context.Notifications.AddAsync(notification, cancellationToken);
    }

    public void Update(Notification notification)
    {
        _context.Notifications.Update(notification);
    }
}
