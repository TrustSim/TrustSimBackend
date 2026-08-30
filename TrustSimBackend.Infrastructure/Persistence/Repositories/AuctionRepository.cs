using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

/// <summary>
/// Triển khai IAuctionRepository bằng EF Core trên TrustSimDbContext.
/// </summary>
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
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, cancellationToken);
    }

    public async Task<Auction?> GetByCodeAsync(string auctionCode, CancellationToken cancellationToken = default)
    {
        return await _context.Auctions
            .FirstOrDefaultAsync(a => a.AuctionCode == auctionCode && !a.IsDeleted, cancellationToken);
    }

    public async Task<Auction?> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Auctions
            .Include(a => a.Author)
                .ThenInclude(u => u!.UserProfile)
            .Include(a => a.SimNumber)
                .ThenInclude(s => s!.PhonePrefix)
                    .ThenInclude(p => p!.SimCarrier)
            .Include(a => a.SimNumber)
                .ThenInclude(s => s!.FormatRhythm)
            .Include(a => a.SimNumber)
                .ThenInclude(s => s!.SimCategoryMappings)
                    .ThenInclude(m => m.SimCategory)
            .Include(a => a.AuctionRule)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, cancellationToken);
    }

    public async Task<bool> ExistsActiveAuctionForSimAsync(Guid simNumberId, CancellationToken cancellationToken = default)
    {
        return await _context.Auctions.AnyAsync(a =>
            a.SimNumberId == simNumberId &&
            !a.IsDeleted &&
            (a.AuctionStatus == AuctionStatus.SCHEDULE || a.AuctionStatus == AuctionStatus.ACTIVE),
            cancellationToken);
    }

    public async Task AddAsync(Auction auction, CancellationToken cancellationToken = default)
    {
        await _context.Auctions.AddAsync(auction, cancellationToken);
    }

    public void Update(Auction auction)
    {
        _context.Auctions.Update(auction);
    }

    public async Task<(List<Auction> Items, int TotalCount)> GetPublicPagedAsync(
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
        // Chỉ hiện thị phiên SCHEDULE và ACTIVE cho public
        var allowedStatuses = new[] { AuctionStatus.SCHEDULE, AuctionStatus.ACTIVE };
        var query = _context.Auctions
            .Include(a => a.SimNumber)
                .ThenInclude(s => s!.PhonePrefix)
                    .ThenInclude(p => p!.SimCarrier)
            .Include(a => a.SimNumber)
                .ThenInclude(s => s!.SimCategoryMappings)
                    .ThenInclude(m => m.SimCategory)
            .Include(a => a.AuctionRule)
            .Where(a => !a.IsDeleted && allowedStatuses.Contains(a.AuctionStatus));

        if (auctionType.HasValue)
            query = query.Where(a => a.AuctionType == auctionType.Value);

        if (status.HasValue && allowedStatuses.Contains(status.Value))
            query = query.Where(a => a.AuctionStatus == status.Value);

        if (carrierId.HasValue)
            query = query.Where(a => a.SimNumber!.PhonePrefix != null && a.SimNumber.PhonePrefix.SimCarrierId == carrierId.Value);

        if (categoryId.HasValue)
            query = query.Where(a => a.SimNumber!.SimCategoryMappings.Any(m => m.SimCategoryId == categoryId.Value));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a =>
                a.AuctionCode.Contains(term) ||
                a.SimNumber!.PhoneNumber.Contains(term));
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
            .OrderByDescending(a => a.CreatedDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(List<Auction> Items, int TotalCount)> GetMyAuctionsPagedAsync(
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
            .Include(a => a.AuctionRule)
            .Where(a => a.AuthorId == authorId && !a.IsDeleted);

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

    public async Task<(List<Auction> Items, int TotalCount)> GetAdminPagedAsync(
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
            .Include(a => a.Author)
                .ThenInclude(u => u!.UserProfile)
            .Include(a => a.SimNumber)
                .ThenInclude(s => s!.PhonePrefix)
                    .ThenInclude(p => p!.SimCarrier)
            .Include(a => a.AuctionRule)
            .Where(a => !a.IsDeleted);

        if (auctionType.HasValue)
            query = query.Where(a => a.AuctionType == auctionType.Value);

        if (status.HasValue)
            query = query.Where(a => a.AuctionStatus == status.Value);

        if (authorId.HasValue)
            query = query.Where(a => a.AuthorId == authorId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a =>
                a.AuctionCode.Contains(term) ||
                a.SimNumber!.PhoneNumber.Contains(term));
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

    public async Task<int> CountActiveAuctionsByAuthorAsync(Guid authorId, CancellationToken cancellationToken = default)
    {
        return await _context.Auctions.CountAsync(a =>
            a.AuthorId == authorId &&
            !a.IsDeleted &&
            (a.AuctionStatus == AuctionStatus.SCHEDULE || a.AuctionStatus == AuctionStatus.ACTIVE),
            cancellationToken);
    }

    public async Task<string> GenerateUniqueAuctionCodeAsync(CancellationToken cancellationToken = default)
    {
        // Format: AUC-YYYYMMDD-XXXX (XXXX: số thứ tự trong ngày)
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var prefix = $"AUC-{today}-";
        var countToday = await _context.Auctions
            .CountAsync(a => a.AuctionCode.StartsWith(prefix), cancellationToken);
        var sequence = (countToday + 1).ToString("D4");
        return $"{prefix}{sequence}";
    }
}
