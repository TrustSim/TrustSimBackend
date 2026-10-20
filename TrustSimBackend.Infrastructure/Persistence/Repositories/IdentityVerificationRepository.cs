using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence.Extensions;
using TrustSimBackend.Infrastructure.Persistence.SortMappings;

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

    public async Task<(IReadOnlyList<IdentityVerification> Items, int TotalCount)> GetPagedHistoryByUserIdAsync(
        Guid userId,
        BaseQueryFilter<KycSortBy> filter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.IdentityVerifications
            .Include(v => v.UserProfile)
            .Include(v => v.VerifiedByUser)
            .AsNoTracking()
            .Where(v => v.UserProfile != null && v.UserProfile.UserAccountId == userId);

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var clean = filter.Keyword.Trim().ToLower();
            query = query.Where(v =>
                (v.RejectionReason != null && v.RejectionReason.ToLower().Contains(clean)) ||
                (v.VerificationMethod != null && v.VerificationMethod.ToLower().Contains(clean)));
        }

        query = query.ApplyDateRange(v => v.SubmittedAt, filter.FromDate, filter.ToDate);
        query = query.ApplySafeSorting(UserSortMappings.KycMappings, filter.SortBy, filter.SortOrder, defaultSortExpression: v => v.SubmittedAt, defaultSortOrder: SortOrder.Desc);

        return await query.ToPagedTupleAsync(filter.PageIndex, filter.PageSize, cancellationToken);
    }

    public async Task<(IReadOnlyList<IdentityVerification> Items, int TotalCount)> GetPagedAsync(
        AdminKycQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.IdentityVerifications
            .Include(v => v.UserProfile)
                .ThenInclude(u => u!.UserAccount)
            .Include(v => v.VerifiedByUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var clean = filter.Keyword.Trim().ToLower();
            query = query.Where(v =>
                (v.UserProfile != null && v.UserProfile.IdentityNumber.ToLower().Contains(clean)) ||
                (v.UserProfile != null && v.UserProfile.FullName.ToLower().Contains(clean)) ||
                (v.UserProfile != null && v.UserProfile.UserAccount != null && v.UserProfile.UserAccount.UserName != null && v.UserProfile.UserAccount.UserName.ToLower().Contains(clean)) ||
                (v.UserProfile != null && v.UserProfile.UserAccount != null && v.UserProfile.UserAccount.Email != null && v.UserProfile.UserAccount.Email.ToLower().Contains(clean)) ||
                (v.UserProfile != null && v.UserProfile.PhoneNumber.Contains(clean)));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(v => v.VerificationStatus == filter.Status.Value);
        }

        if (filter.MinSimilarityScore.HasValue)
        {
            query = query.Where(v => v.FaceSimilarityScore >= filter.MinSimilarityScore.Value);
        }

        query = query.ApplyDateRange(v => v.SubmittedAt, filter.FromDate, filter.ToDate);
        query = query.ApplySafeSorting(UserSortMappings.KycMappings, filter.SortBy, filter.SortOrder, defaultSortExpression: v => v.SubmittedAt, defaultSortOrder: SortOrder.Desc);

        return await query.ToPagedTupleAsync(filter.PageIndex, filter.PageSize, cancellationToken);
    }

    public async Task AddAsync(IdentityVerification verification, CancellationToken cancellationToken = default)
    {
        await _context.IdentityVerifications.AddAsync(verification, cancellationToken);
    }

    public void Update(IdentityVerification verification)
    {
        var entry = _context.Entry(verification);
        if (entry.State == EntityState.Detached)
        {
            _context.IdentityVerifications.Update(verification);
        }
    }
}

