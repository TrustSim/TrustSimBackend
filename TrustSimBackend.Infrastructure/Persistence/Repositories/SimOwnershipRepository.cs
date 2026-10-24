using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence.Extensions;
using TrustSimBackend.Infrastructure.Persistence.SortMappings;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

public class SimOwnershipRepository : ISimOwnershipRepository
{
    private readonly TrustSimDbContext _context;

    public SimOwnershipRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<SimOwnership?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SimOwnerships
            .Include(o => o.SimNumber)
                .ThenInclude(s => s!.PhonePrefix)
                    .ThenInclude(p => p!.SimCarrier)
            .Include(o => o.UserAccount)
                .ThenInclude(u => u!.UserProfile)
            .Include(o => o.SimVerifications)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<SimOwnership?> GetActiveOwnershipByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        return await _context.SimOwnerships
            .Include(o => o.SimNumber)
            .Include(o => o.UserAccount)
            .FirstOrDefaultAsync(o => o.SimNumber != null 
                                   && o.SimNumber.PhoneNumber == phoneNumber 
                                   && o.OwnershipStatus == OwnershipStatus.ACTIVE, cancellationToken);
    }

    public async Task<SimOwnership?> GetActiveOwnershipBySimNumberIdAsync(Guid simNumberId, CancellationToken cancellationToken = default)
    {
        return await _context.SimOwnerships
            .Include(o => o.SimNumber)
            .FirstOrDefaultAsync(o => o.SimNumberId == simNumberId 
                                   && o.OwnershipStatus == OwnershipStatus.ACTIVE, cancellationToken);
    }

    public async Task<SimOwnership?> GetLatestOwnershipByUserAndSimNumberIdAsync(Guid userId, Guid simNumberId, CancellationToken cancellationToken = default)
    {
        return await _context.SimOwnerships
            .Include(o => o.SimNumber)
            .Include(o => o.SimVerifications)
            .Where(o => o.UserAccountId == userId && o.SimNumberId == simNumberId)
            .OrderByDescending(o => o.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SimOwnership>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.SimOwnerships
            .Include(o => o.SimNumber)
                .ThenInclude(s => s!.PhonePrefix)
                    .ThenInclude(p => p!.SimCarrier)
            .Include(o => o.SimNumber)
                .ThenInclude(s => s!.FormatRhythm)
            .Include(o => o.SimVerifications)
            .Where(o => o.UserAccountId == userId)
            .OrderByDescending(o => o.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<SimOwnership> Items, int TotalCount)> GetPagedByUserIdAsync(
        Guid userId,
        SimOwnershipQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SimOwnerships
            .Include(o => o.SimNumber)
                .ThenInclude(s => s!.PhonePrefix)
                    .ThenInclude(p => p!.SimCarrier)
            .Include(o => o.SimNumber)
                .ThenInclude(s => s!.FormatRhythm)
            .Include(o => o.SimVerifications)
            .AsNoTracking()
            .Where(o => o.UserAccountId == userId);

        if (filter.Status.HasValue)
        {
            query = query.Where(o => o.OwnershipStatus == filter.Status.Value);
        }

        if (filter.SimId.HasValue)
        {
            query = query.Where(o => o.SimNumberId == filter.SimId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.Trim().ToLower();
            query = query.Where(o =>
                (o.SimNumber != null && o.SimNumber.PhoneNumber.ToLower().Contains(kw)) ||
                (o.SimNumber != null && o.SimNumber.PhonePrefix != null && o.SimNumber.PhonePrefix.SimCarrier != null && o.SimNumber.PhonePrefix.SimCarrier.CarrierName.ToLower().Contains(kw)));
        }

        query = query.ApplyDateRange(o => o.CreatedDate, filter.FromDate, filter.ToDate);
        query = query.ApplySafeSorting(SimSortMappings.OwnershipMappings, filter.SortBy, filter.SortOrder, defaultSortExpression: o => o.CreatedDate, defaultSortOrder: SortOrder.Desc);

        return await query.ToPagedTupleAsync(filter.PageIndex, filter.PageSize, cancellationToken);
    }

    public async Task<bool> HasActiveOwnershipAsync(string phoneNumber, Guid? excludeUserId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.SimOwnerships
            .Where(o => o.SimNumber != null 
                     && o.SimNumber.PhoneNumber == phoneNumber 
                     && o.OwnershipStatus == OwnershipStatus.ACTIVE);

        if (excludeUserId.HasValue)
        {
            query = query.Where(o => o.UserAccountId != excludeUserId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(SimOwnership ownership, CancellationToken cancellationToken = default)
    {
        await _context.SimOwnerships.AddAsync(ownership, cancellationToken);
    }

    public void Update(SimOwnership ownership)
    {
        _context.SimOwnerships.Update(ownership);
    }
}
