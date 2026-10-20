using System.Linq.Expressions;
using TrustSimBackend.Domain.Entities.Dispute;
using TrustSimBackend.Domain.Entities.System;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Infrastructure.Persistence.SortMappings;

/// <summary>
/// Cấu hình Whitelist Mapping sắp xếp cho Hệ thống, Giám sát & Thông báo (AuditLog, DisputeTicket, Notification).
/// </summary>
public static class SystemSortMappings
{
    /// <summary>
    /// Sắp xếp cho Nhật ký kiểm toán hệ thống (AuditLog).
    /// </summary>
    public static readonly IReadOnlyDictionary<AuditLogSortBy, Expression<Func<AuditLog, object>>> AuditLogMappings =
        new Dictionary<AuditLogSortBy, Expression<Func<AuditLog, object>>>
        {
            [AuditLogSortBy.CreatedDate] = a => a.CreatedDate,
            [AuditLogSortBy.EntityName] = a => a.EntityName,
            [AuditLogSortBy.ActionType] = a => a.ActionType
        };

    /// <summary>
    /// Sắp xếp cho Khiếu nại tranh chấp hợp đồng SIM (DisputeTicket).
    /// </summary>
    public static readonly IReadOnlyDictionary<DisputeTicketSortBy, Expression<Func<DisputeTicket, object>>> DisputeMappings =
        new Dictionary<DisputeTicketSortBy, Expression<Func<DisputeTicket, object>>>
        {
            [DisputeTicketSortBy.CreatedDate] = d => d.CreatedDate,
            [DisputeTicketSortBy.Status] = d => d.Status,
            [DisputeTicketSortBy.TradeTransactionId] = d => d.TradeTransactionId
        };

    /// <summary>
    /// Sắp xếp cho Hộp thư thông báo cá nhân (Notification).
    /// </summary>
    public static readonly IReadOnlyDictionary<NotificationSortBy, Expression<Func<Notification, object>>> NotificationMappings =
        new Dictionary<NotificationSortBy, Expression<Func<Notification, object>>>
        {
            [NotificationSortBy.CreatedDate] = n => n.CreatedDate,
            [NotificationSortBy.IsRead] = n => n.IsRead
        };
}
