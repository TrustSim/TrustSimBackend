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
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<UserAccount?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        return await _context.UserAccounts
            .Include(u => u.UserProfile)
            .Include(u => u.Wallet)
            .FirstOrDefaultAsync(u => u.UserName.ToLower() == userName.ToLower(), cancellationToken);
    }

    public async Task<UserAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.UserAccounts
            .Include(u => u.UserProfile)
            .Include(u => u.Wallet)
            .FirstOrDefaultAsync(u => u.UserEmail.ToLower() == email.ToLower(), cancellationToken);
    }

    public async Task<UserAccount?> GetByUserNameOrEmailAsync(string identifier, CancellationToken cancellationToken = default)
    {
        var lower = identifier.ToLower();
        return await _context.UserAccounts
            .Include(u => u.UserProfile)
            .Include(u => u.Wallet)
            .FirstOrDefaultAsync(u => u.UserName.ToLower() == lower || u.UserEmail.ToLower() == lower, cancellationToken);
    }

    public async Task<bool> ExistsByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        return await _context.UserAccounts
            .AnyAsync(u => u.UserName.ToLower() == userName.ToLower(), cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.UserAccounts
            .AnyAsync(u => u.UserEmail.ToLower() == email.ToLower(), cancellationToken);
    }

    public async Task AddAsync(UserAccount userAccount, CancellationToken cancellationToken = default)
    {
        await _context.UserAccounts.AddAsync(userAccount, cancellationToken);
    }

    public void Update(UserAccount userAccount)
    {
        _context.UserAccounts.Update(userAccount);
    }
}
