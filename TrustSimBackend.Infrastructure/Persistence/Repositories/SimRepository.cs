using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

/// <summary>
/// Triển khai ISimRepository bằng EF Core trên TrustSimDbContext.
/// </summary>
public class SimRepository : ISimRepository
{
    private readonly TrustSimDbContext _context;

    public SimRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<SimNumber?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SimNumbers
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken);
    }

    public async Task<SimNumber?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        return await _context.SimNumbers
            .Include(s => s.SimOwnerships.Where(o => o.OwnershipStatus == OwnershipStatus.ACTIVE))
            .FirstOrDefaultAsync(s => s.PhoneNumber == phoneNumber && !s.IsDeleted, cancellationToken);
    }

    public async Task<bool> ExistsPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        return await _context.SimNumbers
            .AnyAsync(s => s.PhoneNumber == phoneNumber && !s.IsDeleted, cancellationToken);
    }

    public async Task AddAsync(SimNumber simNumber, CancellationToken cancellationToken = default)
    {
        await _context.SimNumbers.AddAsync(simNumber, cancellationToken);
    }

    public void Update(SimNumber simNumber)
    {
        _context.SimNumbers.Update(simNumber);
    }

    public async Task<SimNumber?> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SimNumbers
            .Include(s => s.PhonePrefix)
                .ThenInclude(p => p!.SimCarrier)
            .Include(s => s.FormatRhythm)
            .Include(s => s.SimCategoryMappings)
                .ThenInclude(m => m.SimCategory)
            .Include(s => s.SimOwnerships)
                .ThenInclude(o => o.UserAccount)
                    .ThenInclude(u => u!.UserProfile)
            .Include(s => s.SimOwnerships)
                .ThenInclude(o => o.SimVerifications)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken);
    }

    public async Task<(List<SimOwnership> Items, int TotalCount)> GetMySimsPagedAsync(
        Guid userAccountId,
        int pageIndex,
        int pageSize,
        SimStatus? status = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SimOwnerships
            .Include(o => o.SimNumber)
                .ThenInclude(s => s!.PhonePrefix)
                    .ThenInclude(p => p!.SimCarrier)
            .Include(o => o.SimNumber)
                .ThenInclude(s => s!.FormatRhythm)
            .Include(o => o.SimNumber)
                .ThenInclude(s => s!.SimCategoryMappings)
                    .ThenInclude(m => m.SimCategory)
            .Include(o => o.SimVerifications)
            .Where(o => o.UserAccountId == userAccountId && o.OwnershipStatus == OwnershipStatus.ACTIVE && !o.SimNumber!.IsDeleted);

        if (status.HasValue)
        {
            query = query.Where(o => o.SimNumber!.SimStatus == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var cleanSearch = search.Trim();
            query = query.Where(o => o.SimNumber!.PhoneNumber.Contains(cleanSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(o => o.CreatedDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(List<SimNumber> Items, int TotalCount)> GetAdminSimsPagedAsync(
        int pageIndex,
        int pageSize,
        SimStatus? status = null,
        Guid? carrierId = null,
        Guid? categoryId = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SimNumbers
            .Include(s => s.PhonePrefix)
                .ThenInclude(p => p!.SimCarrier)
            .Include(s => s.FormatRhythm)
            .Include(s => s.SimCategoryMappings)
                .ThenInclude(m => m.SimCategory)
            .Include(s => s.SimOwnerships.Where(o => o.OwnershipStatus == OwnershipStatus.ACTIVE))
                .ThenInclude(o => o.UserAccount)
                    .ThenInclude(u => u!.UserProfile)
            .Include(s => s.SimOwnerships.Where(o => o.OwnershipStatus == OwnershipStatus.ACTIVE))
                .ThenInclude(o => o.SimVerifications)
            .Where(s => !s.IsDeleted);

        if (status.HasValue)
        {
            query = query.Where(s => s.SimStatus == status.Value);
        }

        if (carrierId.HasValue)
        {
            query = query.Where(s => s.PhonePrefix != null && s.PhonePrefix.SimCarrierId == carrierId.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(s => s.SimCategoryMappings.Any(m => m.SimCategoryId == categoryId.Value));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var cleanSearch = search.Trim();
            query = query.Where(s => s.PhoneNumber.Contains(cleanSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.CreatedDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<SimOwnership?> GetActiveOwnershipAsync(Guid simNumberId, CancellationToken cancellationToken = default)
    {
        return await _context.SimOwnerships
            .Include(o => o.UserAccount)
                .ThenInclude(u => u!.UserProfile)
            .Include(o => o.SimVerifications)
            .FirstOrDefaultAsync(o => o.SimNumberId == simNumberId && o.OwnershipStatus == OwnershipStatus.ACTIVE, cancellationToken);
    }
}
