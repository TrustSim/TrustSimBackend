using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence.Extensions;
using TrustSimBackend.Infrastructure.Persistence.SortMappings;

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
        AdminWalletQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Wallets
            .Include(w => w.UserAccount)
                .ThenInclude(u => u!.UserProfile)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var clean = filter.Keyword.Trim();
            query = query.Where(w =>
                (w.UserAccount != null && w.UserAccount.UserName != null && w.UserAccount.UserName.Contains(clean)) ||
                (w.UserAccount != null && w.UserAccount.Email != null && w.UserAccount.Email.Contains(clean)) ||
                (w.UserAccount != null && w.UserAccount.UserProfile != null && w.UserAccount.UserProfile.FullName.Contains(clean)));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(w => w.WalletStatus == filter.Status.Value);
        }

        if (filter.MinBalance.HasValue)
        {
            query = query.Where(w => w.AvailableBalance >= filter.MinBalance.Value);
        }

        if (filter.MaxBalance.HasValue)
        {
            query = query.Where(w => w.AvailableBalance <= filter.MaxBalance.Value);
        }

        query = query.ApplyDateRange(w => w.CreatedDate, filter.FromDate, filter.ToDate);
        query = query.ApplySafeSorting(WalletSortMappings.WalletMappings, filter.SortBy, filter.SortOrder, defaultSortExpression: w => w.CreatedDate, defaultSortOrder: SortOrder.Desc);

        return await query.ToPagedTupleAsync(filter.PageIndex, filter.PageSize, cancellationToken);
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
