using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Repositories;

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
}
