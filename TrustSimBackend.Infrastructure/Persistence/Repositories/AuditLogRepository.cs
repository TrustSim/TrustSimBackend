using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.System;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence.Extensions;
using TrustSimBackend.Infrastructure.Persistence.SortMappings;

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
        AuditLogQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs
            .Include(a => a.UserAccount)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.Trim().ToLower();
            query = query.Where(a =>
                a.EntityName.ToLower().Contains(kw) ||
                a.AuditLogName.ToLower().Contains(kw) ||
                (a.OldData != null && a.OldData.ToLower().Contains(kw)) ||
                (a.NewData != null && a.NewData.ToLower().Contains(kw)));
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityName))
        {
            query = query.Where(a => a.EntityName.ToLower().Contains(filter.EntityName.ToLower()));
        }

        if (filter.EntityId.HasValue)
        {
            query = query.Where(a => a.EntityId == filter.EntityId.Value);
        }

        if (filter.ActorId.HasValue)
        {
            query = query.Where(a => a.ActorId == filter.ActorId.Value);
        }

        if (filter.ActionType.HasValue)
        {
            query = query.Where(a => a.ActionType == filter.ActionType.Value);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(a => a.CreatedDate >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(a => a.CreatedDate <= filter.ToDate.Value);
        }

        query = query.ApplySafeSorting(SystemSortMappings.AuditLogMappings, filter.SortBy, filter.SortOrder, defaultSortExpression: a => a.CreatedDate, defaultSortOrder: SortOrder.Desc);
        return await query.ToPagedTupleAsync(filter.PageIndex, filter.PageSize, cancellationToken);
    }

    public async Task AddAsync(AuditLog log, CancellationToken cancellationToken = default)
    {
        await _context.AuditLogs.AddAsync(log, cancellationToken);
    }
}
