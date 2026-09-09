using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

public class IdentityVerificationRepository : IIdentityVerificationRepository
{
    private readonly TrustSimDbContext _context;

    public IdentityVerificationRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<IdentityVerification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.IdentityVerifications
            .Include(v => v.UserProfile)
                .ThenInclude(u => u!.UserAccount)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<IdentityVerification?> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.IdentityVerifications
            .Include(v => v.UserProfile)
                .ThenInclude(u => u!.UserAccount)
            .Include(v => v.VerifiedByUser)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<IdentityVerification?> GetLatestByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.IdentityVerifications
            .Include(v => v.UserProfile)
            .Where(v => v.UserProfile != null && v.UserProfile.UserAccountId == userId)
            .OrderByDescending(v => v.SubmittedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IdentityVerification>> GetHistoryByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.IdentityVerifications
            .Include(v => v.UserProfile)
            .Include(v => v.VerifiedByUser)
            .Where(v => v.UserProfile != null && v.UserProfile.UserAccountId == userId)
            .OrderByDescending(v => v.SubmittedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<IdentityVerification> Items, int TotalCount)> GetPagedAsync(
        VerificationStatus? status,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.IdentityVerifications
            .Include(v => v.UserProfile)
                .ThenInclude(u => u!.UserAccount)
            .Include(v => v.VerifiedByUser)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(v => v.VerificationStatus == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(v => v.SubmittedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(IdentityVerification verification, CancellationToken cancellationToken = default)
    {
        await _context.IdentityVerifications.AddAsync(verification, cancellationToken);
    }

    public void Update(IdentityVerification verification)
    {
        _context.IdentityVerifications.Update(verification);
    }
}

