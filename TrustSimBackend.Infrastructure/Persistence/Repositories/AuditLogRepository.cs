using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.System;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly TrustSimDbContext _context;

    public AuditLogRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .Include(a => a.UserAccount)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetPagedAsync(
        string? entityName = null,
        Guid? entityId = null,
        Guid? actorId = null,
        ActionType? actionType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs
            .Include(a => a.UserAccount)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            query = query.Where(a => a.EntityName.ToLower().Contains(entityName.ToLower()));
        }

        if (entityId.HasValue)
        {
            query = query.Where(a => a.EntityId == entityId.Value);
        }

        if (actorId.HasValue)
        {
            query = query.Where(a => a.ActorId == actorId.Value);
        }

        if (actionType.HasValue)
        {
            query = query.Where(a => a.ActionType == actionType.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.CreatedDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.CreatedDate <= toDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.CreatedDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(AuditLog log, CancellationToken cancellationToken = default)
    {
        await _context.AuditLogs.AddAsync(log, cancellationToken);
    }
}
