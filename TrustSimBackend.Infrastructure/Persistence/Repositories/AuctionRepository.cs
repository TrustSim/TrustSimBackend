using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

public class AuctionRepository : IAuctionRepository
{
    private readonly TrustSimDbContext _context;

    public AuctionRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<Auction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Auctions
            .Include(a => a.SimNumber)
                .ThenInclude(s => s!.PhonePrefix)
                    .ThenInclude(p => p!.SimCarrier)
            .Include(a => a.Author)
                .ThenInclude(u => u!.UserProfile)
            .Include(a => a.AuctionRule)
            .Include(a => a.Participants)
            .Include(a => a.Bids)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, cancellationToken);
    }

    public async Task<Auction?> GetByCodeAsync(string auctionCode, CancellationToken cancellationToken = default)
    {
        return await _context.Auctions
            .Include(a => a.SimNumber)
            .Include(a => a.Author)
            .Include(a => a.AuctionRule)
            .Include(a => a.Participants)
            .Include(a => a.Bids)
            .FirstOrDefaultAsync(a => a.AuctionCode == auctionCode && !a.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<Auction> Items, int TotalCount)> GetPagedAsync(
        string? keyword,
        AuctionStatus? status,
        AuctionType? type,
        Guid? authorId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Auctions
            .Include(a => a.SimNumber)
                .ThenInclude(s => s!.PhonePrefix)
                    .ThenInclude(p => p!.SimCarrier)
            .Include(a => a.Author)
                .ThenInclude(u => u!.UserProfile)
            .Include(a => a.AuctionRule)
            .Include(a => a.Participants)
            .Include(a => a.Bids)
            .Where(a => !a.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var clean = keyword.Trim();
            query = query.Where(a => a.AuctionCode.Contains(clean) || (a.SimNumber != null && a.SimNumber.PhoneNumber.Contains(clean)));
        }

        if (status.HasValue)
        {
            query = query.Where(a => a.AuctionStatus == status.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(a => a.AuctionType == type.Value);
        }

        if (authorId.HasValue)
        {
            query = query.Where(a => a.AuthorId == authorId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.StartAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> ExistsByCodeAsync(string auctionCode, CancellationToken cancellationToken = default)
    {
        return await _context.Auctions.AnyAsync(a => a.AuctionCode == auctionCode && !a.IsDeleted, cancellationToken);
    }

    public async Task AddAsync(Auction auction, CancellationToken cancellationToken = default)
    {
        await _context.Auctions.AddAsync(auction, cancellationToken);
    }

    public void Update(Auction auction)
    {
        _context.Auctions.Update(auction);
    }

    public async Task<AuctionParticipant?> GetParticipantAsync(Guid auctionId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.AuctionParticipants
            .FirstOrDefaultAsync(p => p.AuctionId == auctionId && p.UserAccountId == userId, cancellationToken);
    }

    public async Task AddParticipantAsync(AuctionParticipant participant, CancellationToken cancellationToken = default)
    {
        await _context.AuctionParticipants.AddAsync(participant, cancellationToken);
    }

    public void UpdateParticipant(AuctionParticipant participant)
    {
        _context.AuctionParticipants.Update(participant);
    }
}
