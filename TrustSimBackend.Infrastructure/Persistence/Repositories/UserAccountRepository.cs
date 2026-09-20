using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Repositories;

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
        string? keyword = null,
        Domain.Enums.UserRole? role = null,
        bool? isActive = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _context.UserAccounts
            .Include(u => u.UserProfile)
            .Include(u => u.Wallet)
            .Include(u => u.UserSecurityProfile)
            .AsNoTracking()
            .Where(u => !u.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(u =>
                (u.UserName != null && u.UserName.ToLower().Contains(kw)) ||
                (u.Email != null && u.Email.ToLower().Contains(kw)) ||
                (u.UserProfile != null && u.UserProfile.FullName.ToLower().Contains(kw)) ||
                (u.UserProfile != null && u.UserProfile.PhoneNumber.Contains(kw)));
        }

        if (role.HasValue)
        {
            query = query.Where(u => u.Role == role.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(u => u.CreatedDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
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
