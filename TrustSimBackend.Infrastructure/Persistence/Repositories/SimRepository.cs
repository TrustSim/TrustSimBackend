using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence.Extensions;
using TrustSimBackend.Infrastructure.Persistence.SortMappings;

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
        MySimsQueryFilter filter,
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

        if (filter.Status.HasValue)
        {
            query = query.Where(o => o.SimNumber!.SimStatus == filter.Status.Value);
        }

        if (filter.CarrierId.HasValue)
        {
            query = query.Where(o => o.SimNumber!.PhonePrefix != null && o.SimNumber!.PhonePrefix.SimCarrierId == filter.CarrierId.Value);
        }

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(o => o.SimNumber!.SimCategoryMappings.Any(m => m.SimCategoryId == filter.CategoryId.Value));
        }

        if (filter.MinPrice.HasValue)
        {
            query = query.Where(o => o.SimNumber!.Price >= filter.MinPrice.Value);
        }

        if (filter.MaxPrice.HasValue)
        {
            query = query.Where(o => o.SimNumber!.Price <= filter.MaxPrice.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var cleanSearch = filter.Keyword.Trim();
            query = query.Where(o => o.SimNumber!.PhoneNumber.Contains(cleanSearch));
        }

        query = query.ApplyDateRange(o => o.CreatedDate, filter.FromDate, filter.ToDate);
        query = query.ApplySafeSorting(SimSortMappings.MySimMappings, filter.SortBy, filter.SortOrder, defaultSortExpression: o => o.CreatedDate, defaultSortOrder: SortOrder.Desc);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((filter.PageIndex - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(List<SimNumber> Items, int TotalCount)> GetAdminSimsPagedAsync(
        AdminSimsQueryFilter filter,
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

        if (filter.Status.HasValue)
        {
            query = query.Where(s => s.SimStatus == filter.Status.Value);
        }

        if (filter.CarrierId.HasValue)
        {
            query = query.Where(s => s.PhonePrefix != null && s.PhonePrefix.SimCarrierId == filter.CarrierId.Value);
        }

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(s => s.SimCategoryMappings.Any(m => m.SimCategoryId == filter.CategoryId.Value));
        }

        if (filter.PrefixId.HasValue)
        {
            query = query.Where(s => s.PhonePrefixId == filter.PrefixId.Value);
        }

        if (filter.FormatRhythmId.HasValue)
        {
            query = query.Where(s => s.FormatRhythmId == filter.FormatRhythmId.Value);
        }

        if (filter.MinPrice.HasValue)
        {
            query = query.Where(s => s.Price >= filter.MinPrice.Value);
        }

        if (filter.MaxPrice.HasValue)
        {
            query = query.Where(s => s.Price <= filter.MaxPrice.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var cleanSearch = filter.Keyword.Trim();
            query = query.Where(s => s.PhoneNumber.Contains(cleanSearch));
        }

        query = query.ApplyDateRange(s => s.CreatedDate, filter.FromDate, filter.ToDate);
        query = query.ApplySafeSorting(SimSortMappings.AdminSimMappings, filter.SortBy, filter.SortOrder, defaultSortExpression: s => s.CreatedDate, defaultSortOrder: SortOrder.Desc);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((filter.PageIndex - 1) * filter.PageSize)
            .Take(filter.PageSize)
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
