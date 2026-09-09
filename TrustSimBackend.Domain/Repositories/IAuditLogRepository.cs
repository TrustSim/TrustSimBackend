using TrustSimBackend.Domain.Entities.System;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Repositories;

public interface IAuditLogRepository
{
    Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetPagedAsync(
        string? entityName = null,
        Guid? entityId = null,
        Guid? actorId = null,
        ActionType? actionType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
    Task AddAsync(AuditLog log, CancellationToken cancellationToken = default);
}
