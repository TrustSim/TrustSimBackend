using TrustSimBackend.Domain.Entities.System;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý Thông báo người dùng (Notification).
/// </summary>
public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Notification> Items, int TotalCount)> GetPagedByUserIdAsync(
        Guid userId,
        bool? isRead,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);
    void Update(Notification notification);
}
