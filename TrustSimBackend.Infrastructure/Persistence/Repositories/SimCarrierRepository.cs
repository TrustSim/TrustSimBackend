using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

public class SimCarrierRepository : ISimCarrierRepository
{
    private readonly TrustSimDbContext _context;

    public SimCarrierRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SimCarrier>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SimCarriers
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.CarrierName)
            .ToListAsync(cancellationToken);
    }

    public async Task<SimCarrier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SimCarriers
            .Include(c => c.PhonePrefixes.Where(p => !p.IsDeleted))
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);
    }

    public async Task<SimCarrier?> GetByCodeAsync(string carrierCode, CancellationToken cancellationToken = default)
    {
        return await _context.SimCarriers
            .FirstOrDefaultAsync(c => EF.Functions.ILike(c.CarrierCode, carrierCode) && !c.IsDeleted, cancellationToken);
    }

    public async Task<bool> ExistsByCodeAsync(string carrierCode, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.SimCarriers.Where(c => !c.IsDeleted);
        if (excludeId.HasValue)
        {
            query = query.Where(c => c.Id != excludeId.Value);
        }
        return await query.AnyAsync(c => EF.Functions.ILike(c.CarrierCode, carrierCode), cancellationToken);
    }

    public async Task AddAsync(SimCarrier carrier, CancellationToken cancellationToken = default)
    {
        await _context.SimCarriers.AddAsync(carrier, cancellationToken);
    }

    public void Update(SimCarrier carrier)
    {
        _context.SimCarriers.Update(carrier);
    }
}
