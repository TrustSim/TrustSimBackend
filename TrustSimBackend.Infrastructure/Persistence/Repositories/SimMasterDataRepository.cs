using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

/// <summary>
/// Triển khai ISimMasterDataRepository bằng EF Core trên TrustSimDbContext.
/// </summary>
public class SimMasterDataRepository : ISimMasterDataRepository
{
    private readonly TrustSimDbContext _context;

    public SimMasterDataRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    #region SimCarrier
    public async Task<List<SimCarrier>> GetCarriersAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SimCarriers
            .Include(c => c.PhonePrefixes.Where(p => p.IsActive && !p.IsDeleted))
            .Where(c => c.IsActive && !c.IsDeleted)
            .OrderBy(c => c.CarrierName)
            .ToListAsync(cancellationToken);
    }

    public async Task<SimCarrier?> GetCarrierByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SimCarriers
            .Include(c => c.PhonePrefixes.Where(p => p.IsActive && !p.IsDeleted))
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);
    }

    public async Task<SimCarrier?> GetCarrierByCodeAsync(string carrierCode, CancellationToken cancellationToken = default)
    {
        return await _context.SimCarriers
            .Include(c => c.PhonePrefixes.Where(p => p.IsActive && !p.IsDeleted))
            .FirstOrDefaultAsync(c => c.CarrierCode.ToUpper() == carrierCode.ToUpper() && !c.IsDeleted, cancellationToken);
    }

    public async Task AddCarrierAsync(SimCarrier carrier, CancellationToken cancellationToken = default)
    {
        await _context.SimCarriers.AddAsync(carrier, cancellationToken);
    }

    public async Task<bool> ExistsCarrierCodeAsync(string carrierCode, CancellationToken cancellationToken = default)
    {
        return await _context.SimCarriers
            .AnyAsync(c => c.CarrierCode.ToUpper() == carrierCode.ToUpper() && !c.IsDeleted, cancellationToken);
    }
    #endregion

    #region PhonePrefix
    public async Task<List<PhonePrefix>> GetPhonePrefixesAsync(Guid? carrierId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.PhonePrefixes
            .Include(p => p.SimCarrier)
            .Where(p => p.IsActive && !p.IsDeleted);

        if (carrierId.HasValue)
        {
            query = query.Where(p => p.SimCarrierId == carrierId.Value);
        }

        return await query.OrderBy(p => p.PrefixValue).ToListAsync(cancellationToken);
    }

    public async Task<PhonePrefix?> GetPhonePrefixByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PhonePrefixes
            .Include(p => p.SimCarrier)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
    }

    public async Task<PhonePrefix?> GetPhonePrefixByValueAsync(string prefixValue, CancellationToken cancellationToken = default)
    {
        return await _context.PhonePrefixes
            .Include(p => p.SimCarrier)
            .FirstOrDefaultAsync(p => p.PrefixValue == prefixValue && !p.IsDeleted, cancellationToken);
    }

    public async Task AddPhonePrefixAsync(PhonePrefix prefix, CancellationToken cancellationToken = default)
    {
        await _context.PhonePrefixes.AddAsync(prefix, cancellationToken);
    }
    #endregion

    #region SimCategory
    public async Task<List<SimCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SimCategories
            .Where(c => c.IsActive && !c.IsDeleted)
            .OrderByDescending(c => c.ValuationMultiplier)
            .ThenBy(c => c.CategoryName)
            .ToListAsync(cancellationToken);
    }

    public async Task<SimCategory?> GetCategoryByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SimCategories
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);
    }

    public async Task<SimCategory?> GetCategoryByCodeAsync(string categoryCode, CancellationToken cancellationToken = default)
    {
        return await _context.SimCategories
            .FirstOrDefaultAsync(c => c.CategoryCode.ToUpper() == categoryCode.ToUpper() && !c.IsDeleted, cancellationToken);
    }

    public async Task AddCategoryAsync(SimCategory category, CancellationToken cancellationToken = default)
    {
        await _context.SimCategories.AddAsync(category, cancellationToken);
    }

    public async Task<bool> ExistsCategoryCodeAsync(string categoryCode, CancellationToken cancellationToken = default)
    {
        return await _context.SimCategories
            .AnyAsync(c => c.CategoryCode.ToUpper() == categoryCode.ToUpper() && !c.IsDeleted, cancellationToken);
    }
    #endregion

    #region FormatRhythm
    public async Task<List<FormatRhythm>> GetFormatRhythmsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.FormatRhythms
            .Where(f => f.IsActive && !f.IsDeleted)
            .OrderBy(f => f.FormatRhythmName)
            .ToListAsync(cancellationToken);
    }

    public async Task<FormatRhythm?> GetFormatRhythmByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.FormatRhythms
            .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted, cancellationToken);
    }

    public async Task AddFormatRhythmAsync(FormatRhythm formatRhythm, CancellationToken cancellationToken = default)
    {
        await _context.FormatRhythms.AddAsync(formatRhythm, cancellationToken);
    }
    #endregion

    #region Seeding & Checks
    public async Task<bool> HasAnyCarrierAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SimCarriers.AnyAsync(cancellationToken);
    }
    #endregion
}
