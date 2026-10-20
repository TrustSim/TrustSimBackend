using TrustSimBackend.Domain.Entities.System;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Domain.Repositories;

public interface IAuditLogRepository
{
    Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetPagedAsync(
        AuditLogQueryFilter filter,
        CancellationToken cancellationToken = default);
    Task AddAsync(AuditLog log, CancellationToken cancellationToken = default);
}
