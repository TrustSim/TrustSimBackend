using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence.Extensions;
using TrustSimBackend.Infrastructure.Persistence.SortMappings;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

/// <summary>
/// Triển khai IUserAccountRepository sử dụng Entity Framework Core.
/// </summary>
public class UserAccountRepository : IUserAccountRepository
{
    private readonly TrustSimDbContext _context;

    public UserAccountRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.UserAccounts
            .Include(u => u.UserProfile)
            .Include(u => u.Wallet)
            .Include(u => u.UserSecurityProfile)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<UserAccount?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        return await _context.UserAccounts
            .Include(u => u.UserProfile)
            .Include(u => u.Wallet)
            .Include(u => u.UserSecurityProfile)
            .FirstOrDefaultAsync(u => u.UserName != null && EF.Functions.ILike(u.UserName, userName), cancellationToken);
    }

    public async Task<UserAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.UserAccounts
            .Include(u => u.UserProfile)
            .Include(u => u.Wallet)
            .Include(u => u.UserSecurityProfile)
            .FirstOrDefaultAsync(u => u.Email != null && EF.Functions.ILike(u.Email, email), cancellationToken);
    }

    public async Task<UserAccount?> GetByUserNameOrEmailAsync(string identifier, CancellationToken cancellationToken = default)
    {
        return await _context.UserAccounts
            .Include(u => u.UserProfile)
            .Include(u => u.Wallet)
            .Include(u => u.UserSecurityProfile)
            .FirstOrDefaultAsync(u => (u.UserName != null && EF.Functions.ILike(u.UserName, identifier)) || (u.Email != null && EF.Functions.ILike(u.Email, identifier)), cancellationToken);
    }

    public async Task<bool> ExistsByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        return await _context.UserAccounts
            .AnyAsync(u => u.UserName != null && EF.Functions.ILike(u.UserName, userName), cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.UserAccounts
            .AnyAsync(u => u.Email != null && EF.Functions.ILike(u.Email, email), cancellationToken);
    }

    public async Task<bool> ExistsByIdentityNumberAsync(string identityNumber, Guid? excludeUserId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.UserProfiles.AsQueryable();
        if (excludeUserId.HasValue)
        {
            query = query.Where(p => p.UserAccountId != excludeUserId.Value);
        }
        return await query.AnyAsync(p => p.IdentityNumber == identityNumber, cancellationToken);
    }

    public async Task<bool> ExistsByPhoneNumberAsync(string phoneNumber, Guid? excludeUserId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.UserProfiles.AsQueryable();
        if (excludeUserId.HasValue)
        {
            query = query.Where(p => p.UserAccountId != excludeUserId.Value);
        }
        return await query.AnyAsync(p => p.PhoneNumber == phoneNumber, cancellationToken);
    }

    public async Task<(IReadOnlyList<UserAccount> Items, int TotalCount)> GetPagedAsync(
        AdminUsersQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.UserAccounts
            .Include(u => u.UserProfile)
            .Include(u => u.Wallet)
            .Include(u => u.UserSecurityProfile)
            .AsNoTracking()
            .Where(u => !u.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.Trim().ToLower();
            query = query.Where(u =>
                (u.UserName != null && u.UserName.ToLower().Contains(kw)) ||
                (u.Email != null && u.Email.ToLower().Contains(kw)) ||
                (u.UserProfile != null && u.UserProfile.FullName.ToLower().Contains(kw)) ||
                (u.UserProfile != null && u.UserProfile.PhoneNumber.Contains(kw)));
        }

        if (filter.Role.HasValue)
        {
            query = query.Where(u => u.Role == filter.Role.Value);
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == filter.IsActive.Value);
        }

        if (filter.KycStatus.HasValue)
        {
            query = query.Where(u => u.UserProfile != null && u.UserProfile.VerificationStatus == filter.KycStatus.Value);
        }

        if (filter.MinWalletBalance.HasValue)
        {
            query = query.Where(u => u.Wallet != null && u.Wallet.AvailableBalance >= filter.MinWalletBalance.Value);
        }

        if (filter.MaxWalletBalance.HasValue)
        {
            query = query.Where(u => u.Wallet != null && u.Wallet.AvailableBalance <= filter.MaxWalletBalance.Value);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(u => u.CreatedDate >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(u => u.CreatedDate <= filter.ToDate.Value);
        }

        query = query.ApplySafeSorting(UserSortMappings.UserAccountMappings, filter.SortBy, filter.SortOrder, defaultSortExpression: u => u.CreatedDate, defaultSortOrder: SortOrder.Desc);
        return await query.ToPagedTupleAsync(filter.PageIndex, filter.PageSize, cancellationToken);
    }

    public async Task AddAsync(UserAccount userAccount, CancellationToken cancellationToken = default)
    {
        await _context.UserAccounts.AddAsync(userAccount, cancellationToken);
    }

    public void Update(UserAccount userAccount)
    {
        var entry = _context.Entry(userAccount);
        if (entry.State == EntityState.Detached)
        {
            _context.UserAccounts.Update(userAccount);
        }
    }
}
