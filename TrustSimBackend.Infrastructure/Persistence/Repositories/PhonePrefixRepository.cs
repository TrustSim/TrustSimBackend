using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

public class PhonePrefixRepository : IPhonePrefixRepository
{
    private readonly TrustSimDbContext _context;

    public PhonePrefixRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PhonePrefix>> GetAllAsync(Guid? carrierId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.PhonePrefixes
            .Include(p => p.SimCarrier)
            .Where(p => !p.IsDeleted);

        if (carrierId.HasValue)
        {
            query = query.Where(p => p.SimCarrierId == carrierId.Value);
        }

        return await query.OrderBy(p => p.PrefixValue).ToListAsync(cancellationToken);
    }

    public async Task<PhonePrefix?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PhonePrefixes
            .Include(p => p.SimCarrier)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
    }

    public async Task<PhonePrefix?> GetByValueAsync(string prefixValue, CancellationToken cancellationToken = default)
    {
        return await _context.PhonePrefixes
            .Include(p => p.SimCarrier)
            .FirstOrDefaultAsync(p => p.PrefixValue == prefixValue && !p.IsDeleted, cancellationToken);
    }

    public async Task<bool> ExistsByValueAsync(string prefixValue, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.PhonePrefixes.Where(p => !p.IsDeleted);
        if (excludeId.HasValue)
        {
            query = query.Where(p => p.Id != excludeId.Value);
        }
        return await query.AnyAsync(p => p.PrefixValue == prefixValue, cancellationToken);
    }

    public async Task AddAsync(PhonePrefix prefix, CancellationToken cancellationToken = default)
    {
        await _context.PhonePrefixes.AddAsync(prefix, cancellationToken);
    }

    public void Update(PhonePrefix prefix)
    {
        _context.PhonePrefixes.Update(prefix);
    }
}
