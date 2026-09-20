using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

/// <summary>
/// Triển khai IWalletRepository sử dụng EF Core.
/// </summary>
public class WalletRepository : IWalletRepository
{
    private readonly TrustSimDbContext _context;

    public WalletRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Wallets
            .Include(w => w.UserAccount)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Wallets
            .FirstOrDefaultAsync(w => w.UserAccountId == userId, cancellationToken);
    }

    public async Task<bool> ExistsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Wallets
            .AnyAsync(w => w.UserAccountId == userId, cancellationToken);
    }

    public async Task<(IReadOnlyList<Wallet> Items, int TotalCount)> GetPagedAsync(
        string? keyword = null,
        Domain.Enums.WalletStatus? status = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Wallets
            .Include(w => w.UserAccount)
                .ThenInclude(u => u!.UserProfile)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(w =>
                (w.UserAccount != null && w.UserAccount.UserName != null && w.UserAccount.UserName.ToLower().Contains(kw)) ||
                (w.UserAccount != null && w.UserAccount.Email != null && w.UserAccount.Email.ToLower().Contains(kw)) ||
                (w.UserAccount != null && w.UserAccount.UserProfile != null && w.UserAccount.UserProfile.FullName.ToLower().Contains(kw)));
        }

        if (status.HasValue)
        {
            query = query.Where(w => w.WalletStatus == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(w => w.AvailableBalance)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default)
    {
        await _context.Wallets.AddAsync(wallet, cancellationToken);
    }

    public void Update(Wallet wallet)
    {
        _context.Wallets.Update(wallet);
    }
}
