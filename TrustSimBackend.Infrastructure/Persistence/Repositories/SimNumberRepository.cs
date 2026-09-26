using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

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
        string? keyword,
        Guid? carrierId,
        Guid? categoryId,
        Guid? prefixId,
        SimStatus? status,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SimNumbers
            .Include(s => s.PhonePrefix)
                .ThenInclude(p => p!.SimCarrier)
            .Include(s => s.FormatRhythm)
            .Include(s => s.SimCategoryMappings)
                .ThenInclude(m => m.SimCategory)
            .Where(s => !s.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var clean = keyword.Trim();
            query = query.Where(s => s.PhoneNumber.Contains(clean));
        }

        if (status.HasValue)
        {
            query = query.Where(s => s.SimStatus == status.Value);
        }

        if (prefixId.HasValue)
        {
            query = query.Where(s => s.PhonePrefixId == prefixId.Value);
        }

        if (carrierId.HasValue)
        {
            query = query.Where(s => s.PhonePrefix != null && s.PhonePrefix.SimCarrierId == carrierId.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(s => s.SimCategoryMappings.Any(m => m.SimCategoryId == categoryId.Value));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.CreatedDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
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
