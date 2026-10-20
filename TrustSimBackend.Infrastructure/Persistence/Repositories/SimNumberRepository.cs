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

public class SimNumberRepository : ISimNumberRepository
{

    private readonly TrustSimDbContext _context;

    public SimNumberRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<SimNumber?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SimNumbers
            .Include(s => s.PhonePrefix)
                .ThenInclude(p => p!.SimCarrier)
            .Include(s => s.FormatRhythm)
            .Include(s => s.SimCategoryMappings)
                .ThenInclude(m => m.SimCategory)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken);
    }

    public async Task<SimNumber?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        return await _context.SimNumbers
            .Include(s => s.PhonePrefix)
                .ThenInclude(p => p!.SimCarrier)
            .Include(s => s.FormatRhythm)
            .Include(s => s.SimCategoryMappings)
                .ThenInclude(m => m.SimCategory)
            .FirstOrDefaultAsync(s => s.PhoneNumber == phoneNumber && !s.IsDeleted, cancellationToken);
    }

    public async Task<bool> ExistsByPhoneNumberAsync(string phoneNumber, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.SimNumbers.Where(s => !s.IsDeleted);
        if (excludeId.HasValue)
        {
            query = query.Where(s => s.Id != excludeId.Value);
        }
        return await query.AnyAsync(s => s.PhoneNumber == phoneNumber, cancellationToken);
    }

    public async Task<(IReadOnlyList<SimNumber> Items, int TotalCount)> GetPagedAsync(
        SimPublicQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SimNumbers
            .Include(s => s.PhonePrefix)
                .ThenInclude(p => p!.SimCarrier)
            .Include(s => s.FormatRhythm)
            .Include(s => s.SimCategoryMappings)
                .ThenInclude(m => m.SimCategory)
            .Where(s => !s.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var clean = filter.Keyword.Trim();
            query = query.Where(s => s.PhoneNumber.Contains(clean));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(s => s.SimStatus == filter.Status.Value);
        }

        if (filter.PrefixId.HasValue)
        {
            query = query.Where(s => s.PhonePrefixId == filter.PrefixId.Value);
        }

        if (filter.CarrierId.HasValue)
        {
            query = query.Where(s => s.PhonePrefix != null && s.PhonePrefix.SimCarrierId == filter.CarrierId.Value);
        }

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(s => s.SimCategoryMappings.Any(m => m.SimCategoryId == filter.CategoryId.Value));
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

        query = query.ApplyDateRange(s => s.CreatedDate, filter.FromDate, filter.ToDate);
        query = query.ApplySafeSorting(SimSortMappings.PublicSimMappings, filter.SortBy, filter.SortOrder, defaultSortExpression: s => s.CreatedDate, defaultSortOrder: SortOrder.Desc);

        return await query.ToPagedTupleAsync(filter.PageIndex, filter.PageSize, cancellationToken);
    }

    public async Task<Guid> GetOrCreateDefaultFormatRhythmIdAsync(CancellationToken cancellationToken = default)
    {
        var first = await _context.FormatRhythms.Select(f => f.Id).FirstOrDefaultAsync(cancellationToken);
        if (first != Guid.Empty)
        {
            return first;
        }

        var newRhythm = new FormatRhythm
        {
            Id = Guid.NewGuid(),
            FormatRhythmName = "Thường",
            Value = "NORMAL",
            Description = "Số thông thường"
        };
        await _context.FormatRhythms.AddAsync(newRhythm, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return newRhythm.Id;
    }

    public async Task<IReadOnlyList<SimNumber>> GetAvailableSimsForConsultationAsync(
        decimal? minPrice,
        decimal? maxPrice,
        string? carrier = null,
        string? prefix = null,
        string? keyword = null,
        int limit = 6,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SimNumbers
            .Include(s => s.PhonePrefix)
                .ThenInclude(p => p!.SimCarrier)
            .Include(s => s.FormatRhythm)
            .Where(s => !s.IsDeleted && s.SimStatus == SimStatus.IN_AUCTION);

        if (maxPrice.HasValue && maxPrice.Value > 0)
        {
            query = query.Where(s => s.Price <= maxPrice.Value);
        }
        if (minPrice.HasValue && minPrice.Value > 0)
        {
            query = query.Where(s => s.Price >= minPrice.Value);
        }

        if (!string.IsNullOrWhiteSpace(carrier))
        {
            var cleanCarrier = carrier.Trim();
            query = query.Where(s => s.PhonePrefix != null && s.PhonePrefix.SimCarrier != null &&
                (EF.Functions.ILike(s.PhonePrefix.SimCarrier.CarrierName, $"%{cleanCarrier}%") || 
                 EF.Functions.ILike(s.PhonePrefix.SimCarrier.CarrierCode, $"%{cleanCarrier}%")));
        }

        if (!string.IsNullOrWhiteSpace(prefix))
        {
            var cleanPrefix = prefix.Trim();
            query = query.Where(s => (s.PhonePrefix != null && s.PhonePrefix.PrefixValue == cleanPrefix) || s.PhoneNumber.StartsWith(cleanPrefix));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var cleanKeyword = keyword.Trim();
            query = query.Where(s => s.PhoneNumber.Contains(cleanKeyword));
        }

        return await query
            .OrderByDescending(s => s.Price)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(SimNumber simNumber, CancellationToken cancellationToken = default)
    {
        await _context.SimNumbers.AddAsync(simNumber, cancellationToken);
    }

    public void Update(SimNumber simNumber)
    {
        _context.SimNumbers.Update(simNumber);
    }
}
