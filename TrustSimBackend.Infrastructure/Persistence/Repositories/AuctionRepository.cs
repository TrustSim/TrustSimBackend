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

    public async Task<Auction?> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Auctions
            .Include(a => a.SimNumber)
                .ThenInclude(s => s!.PhonePrefix)
                    .ThenInclude(p => p!.SimCarrier)
            .Include(a => a.SimNumber)
                .ThenInclude(s => s!.FormatRhythm)
            .Include(a => a.SimNumber)
                .ThenInclude(s => s!.SimCategoryMappings)
                    .ThenInclude(m => m.SimCategory)
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

    public async Task<(IReadOnlyList<Auction> Items, int TotalCount)> GetPublicPagedAsync(
        int pageIndex,
        int pageSize,
        AuctionType? auctionType = null,
        AuctionStatus? status = null,
        Guid? carrierId = null,
        Guid? categoryId = null,
        string? search = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        DateTime? startFrom = null,
        DateTime? startTo = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Auctions
            .Include(a => a.SimNumber)
                .ThenInclude(s => s!.PhonePrefix)
                    .ThenInclude(p => p!.SimCarrier)
            .Include(a => a.SimNumber)
                .ThenInclude(s => s!.FormatRhythm)
            .Include(a => a.SimNumber)
                .ThenInclude(s => s!.SimCategoryMappings)
            .Where(a => !a.IsDeleted);

        if (status.HasValue)
        {
            query = query.Where(a => a.AuctionStatus == status.Value);
        }
        else
        {
            query = query.Where(a => a.AuctionStatus == AuctionStatus.SCHEDULE || a.AuctionStatus == AuctionStatus.ACTIVE);
        }

        if (auctionType.HasValue)
            query = query.Where(a => a.AuctionType == auctionType.Value);

        if (carrierId.HasValue)
            query = query.Where(a => a.SimNumber != null && a.SimNumber.PhonePrefix != null && a.SimNumber.PhonePrefix.SimCarrierId == carrierId.Value);

        if (categoryId.HasValue)
            query = query.Where(a => a.SimNumber != null && a.SimNumber.SimCategoryMappings.Any(m => m.SimCategoryId == categoryId.Value));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var clean = search.Trim();
            query = query.Where(a => a.AuctionCode.Contains(clean) || (a.SimNumber != null && a.SimNumber.PhoneNumber.Contains(clean)));
        }

        if (minPrice.HasValue)
            query = query.Where(a => a.CurrentPrice >= minPrice.Value);

        if (maxPrice.HasValue)
            query = query.Where(a => a.CurrentPrice <= maxPrice.Value);

        if (startFrom.HasValue)
            query = query.Where(a => a.StartAt >= startFrom.Value);

        if (startTo.HasValue)
            query = query.Where(a => a.StartAt <= startTo.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.StartAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<Auction> Items, int TotalCount)> GetMyAuctionsPagedAsync(
        Guid authorId,
        int pageIndex,
        int pageSize,
        AuctionStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Auctions
            .Include(a => a.SimNumber)
                .ThenInclude(s => s!.PhonePrefix)
                    .ThenInclude(p => p!.SimCarrier)
            .Include(a => a.SimNumber)
                .ThenInclude(s => s!.FormatRhythm)
            .Where(a => !a.IsDeleted && a.AuthorId == authorId);

        if (status.HasValue)
            query = query.Where(a => a.AuctionStatus == status.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.CreatedDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<Auction> Items, int TotalCount)> GetAdminPagedAsync(
        int pageIndex,
        int pageSize,
        AuctionType? auctionType = null,
        AuctionStatus? status = null,
        Guid? authorId = null,
        string? search = null,
        DateTime? startFrom = null,
        DateTime? startTo = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Auctions
            .Include(a => a.SimNumber)
                .ThenInclude(s => s!.PhonePrefix)
                    .ThenInclude(p => p!.SimCarrier)
            .Include(a => a.SimNumber)
                .ThenInclude(s => s!.FormatRhythm)
            .Include(a => a.Author)
                .ThenInclude(u => u!.UserProfile)
            .Where(a => !a.IsDeleted);

        if (auctionType.HasValue)
            query = query.Where(a => a.AuctionType == auctionType.Value);

        if (status.HasValue)
            query = query.Where(a => a.AuctionStatus == status.Value);

        if (authorId.HasValue)
            query = query.Where(a => a.AuthorId == authorId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var clean = search.Trim();
            query = query.Where(a => a.AuctionCode.Contains(clean) || (a.SimNumber != null && a.SimNumber.PhoneNumber.Contains(clean)));
        }

        if (startFrom.HasValue)
            query = query.Where(a => a.StartAt >= startFrom.Value);

        if (startTo.HasValue)
            query = query.Where(a => a.StartAt <= startTo.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.CreatedDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> ExistsByCodeAsync(string auctionCode, CancellationToken cancellationToken = default)
    {
        return await _context.Auctions.AnyAsync(a => a.AuctionCode == auctionCode && !a.IsDeleted, cancellationToken);
    }

    public async Task<bool> ExistsActiveAuctionForSimAsync(Guid simNumberId, CancellationToken cancellationToken = default)
    {
        return await _context.Auctions.AnyAsync(a =>
            a.SimNumberId == simNumberId &&
            !a.IsDeleted &&
            (a.AuctionStatus == AuctionStatus.SCHEDULE || a.AuctionStatus == AuctionStatus.ACTIVE),
            cancellationToken);
    }

    public async Task<string> GenerateUniqueAuctionCodeAsync(CancellationToken cancellationToken = default)
    {
        string code;
        do
        {
            code = "AUC" + Random.Shared.Next(100000, 999999);
        } while (await ExistsByCodeAsync(code, cancellationToken));

        return code;
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

