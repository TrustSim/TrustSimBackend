using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

public class SimVerificationRepository : ISimVerificationRepository
{
    private readonly TrustSimDbContext _context;

    public SimVerificationRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<SimVerification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SimVerifications
            .Include(v => v.SimOwnership)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<SimVerification>> GetByOwnershipIdAsync(Guid simOwnershipId, CancellationToken cancellationToken = default)
    {
        return await _context.SimVerifications
            .Where(v => v.SimOwnershipId == simOwnershipId)
            .OrderByDescending(v => v.RequestedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SimVerification>> GetBySimNumberIdAsync(Guid simNumberId, CancellationToken cancellationToken = default)
    {
        return await _context.SimVerifications
            .Include(v => v.SimOwnership)
            .Where(v => v.SimOwnership != null && v.SimOwnership.SimNumberId == simNumberId)
            .OrderByDescending(v => v.RequestedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(SimVerification verification, CancellationToken cancellationToken = default)
    {
        await _context.SimVerifications.AddAsync(verification, cancellationToken);
    }

    public void Update(SimVerification verification)
    {
        _context.SimVerifications.Update(verification);
    }
}
