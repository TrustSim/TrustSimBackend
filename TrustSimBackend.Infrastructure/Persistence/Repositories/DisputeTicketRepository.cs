using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Dispute;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

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
        DisputeStatus? status,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.DisputeTickets
            .Include(d => d.Complainant)
                .ThenInclude(u => u!.UserProfile)
            .Include(d => d.ResolvedByAdmin)
                .ThenInclude(u => u!.UserProfile)
            .AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(d => d.ComplainantId == userId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(d => d.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(d => d.CreatedDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
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
