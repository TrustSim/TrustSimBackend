using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Dispute;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence.Extensions;
using TrustSimBackend.Infrastructure.Persistence.SortMappings;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

public class DisputeTicketRepository : IDisputeTicketRepository
{
    private readonly TrustSimDbContext _context;

    public DisputeTicketRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<DisputeTicket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.DisputeTickets
            .Include(d => d.Complainant)
                .ThenInclude(u => u!.UserProfile)
            .Include(d => d.ResolvedByAdmin)
                .ThenInclude(u => u!.UserProfile)
            .Include(d => d.TradeTransaction)
            .Include(d => d.Proofs)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<DisputeTicket> Items, int TotalCount)> GetPagedAsync(
        Guid? userId,
        DisputeQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.DisputeTickets
            .Include(d => d.Complainant)
                .ThenInclude(u => u!.UserProfile)
            .Include(d => d.ResolvedByAdmin)
                .ThenInclude(u => u!.UserProfile)
            .AsNoTracking()
            .AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(d => d.ComplainantId == userId.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(d => d.Status == filter.Status.Value);
        }

        if (filter.TradeId.HasValue)
        {
            query = query.Where(d => d.TradeTransactionId == filter.TradeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.Trim().ToLower();
            query = query.Where(d =>
                d.Reason.ToLower().Contains(kw) ||
                (d.AdminResolutionNotes != null && d.AdminResolutionNotes.ToLower().Contains(kw)) ||
                (d.Complainant != null && d.Complainant.UserProfile != null && d.Complainant.UserProfile.FullName.ToLower().Contains(kw)));
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(d => d.CreatedDate >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(d => d.CreatedDate <= filter.ToDate.Value);
        }

        query = query.ApplySafeSorting(SystemSortMappings.DisputeMappings, filter.SortBy, filter.SortOrder, defaultSortExpression: d => d.CreatedDate, defaultSortOrder: SortOrder.Desc);
        return await query.ToPagedTupleAsync(filter.PageIndex, filter.PageSize, cancellationToken);
    }

    public async Task AddAsync(DisputeTicket ticket, CancellationToken cancellationToken = default)
    {
        await _context.DisputeTickets.AddAsync(ticket, cancellationToken);
    }

    public void Update(DisputeTicket ticket)
    {
        _context.DisputeTickets.Update(ticket);
    }
}
