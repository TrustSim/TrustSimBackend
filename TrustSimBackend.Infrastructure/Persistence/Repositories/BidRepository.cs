using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence.Extensions;
using TrustSimBackend.Infrastructure.Persistence.SortMappings;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

public class BidRepository : IBidRepository
{
    private readonly TrustSimDbContext _context;

    public BidRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Bid>> GetBidsByAuctionIdAsync(Guid auctionId, int top = 50, CancellationToken cancellationToken = default)
    {
        return await _context.Bids
            .Include(b => b.Participant)
                .ThenInclude(p => p!.UserAccount)
                    .ThenInclude(u => u!.UserProfile)
            .Where(b => b.AuctionId == auctionId)
            .OrderByDescending(b => b.BidAmount)
            .Take(top)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Bid> Items, int TotalCount)> GetPagedBidsByAuctionIdAsync(
        Guid auctionId,
        AuctionBidQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Bids
            .Include(b => b.Participant)
                .ThenInclude(p => p!.UserAccount)
                    .ThenInclude(u => u!.UserProfile)
            .AsNoTracking()
            .Where(b => b.AuctionId == auctionId);

        if (filter.Status.HasValue)
        {
            query = query.Where(b => b.Status == filter.Status.Value);
        }

        if (filter.MinBidAmount.HasValue)
        {
            query = query.Where(b => b.BidAmount >= filter.MinBidAmount.Value);
        }

        if (filter.MaxBidAmount.HasValue)
        {
            query = query.Where(b => b.BidAmount <= filter.MaxBidAmount.Value);
        }

        query = query.ApplyDateRange(b => b.PlacedAt, filter.FromDate, filter.ToDate);
        query = query.ApplySafeSorting(BidSortMappings.BidMappings, filter.SortBy, filter.SortOrder, defaultSortExpression: b => b.BidAmount, defaultSortOrder: SortOrder.Desc);

        return await query.ToPagedTupleAsync(filter.PageIndex, filter.PageSize, cancellationToken);
    }

    public async Task<Bid?> GetHighestBidAsync(Guid auctionId, CancellationToken cancellationToken = default)
    {
        return await _context.Bids
            .Include(b => b.Participant)
            .Where(b => b.AuctionId == auctionId)
            .OrderByDescending(b => b.BidAmount)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(Bid bid, CancellationToken cancellationToken = default)
    {
        await _context.Bids.AddAsync(bid, cancellationToken);
    }

    public void Update(Bid bid)
    {
        _context.Bids.Update(bid);
    }

    public async Task<int> CountByAuctionIdAsync(Guid auctionId, CancellationToken cancellationToken = default)
    {
        return await _context.Bids.CountAsync(b => b.AuctionId == auctionId, cancellationToken);
    }
}
