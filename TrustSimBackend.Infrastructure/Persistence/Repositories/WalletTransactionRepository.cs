using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence.Extensions;
using TrustSimBackend.Infrastructure.Persistence.SortMappings;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

/// <summary>
/// Triển khai IWalletTransactionRepository sử dụng EF Core.
/// </summary>
public class WalletTransactionRepository : IWalletTransactionRepository
{
    private readonly TrustSimDbContext _context;

    public WalletTransactionRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<WalletTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.WalletTransactions
            .Include(t => t.Wallet)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<WalletTransaction?> GetByTransactionCodeAsync(string transactionCode, CancellationToken cancellationToken = default)
    {
        var cleanCode = transactionCode.Trim();
        // 1. Thử tìm chính xác (case-insensitive)
        var transaction = await _context.WalletTransactions
            .Include(t => t.Wallet)
            .FirstOrDefaultAsync(t => EF.Functions.ILike(t.TransactionCode, cleanCode), cancellationToken);

        if (transaction != null) return transaction;

        // 2. Thử tìm theo prefix hoặc tiền tố (nếu SePay trích xuất code ngắn hơn hoặc dài hơn)
        return await _context.WalletTransactions
            .Include(t => t.Wallet)
            .FirstOrDefaultAsync(t => 
                EF.Functions.ILike(t.TransactionCode, cleanCode + "%") || 
                EF.Functions.ILike(cleanCode, t.TransactionCode + "%"), 
                cancellationToken);
    }

    public async Task<WalletTransaction?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        return await _context.WalletTransactions
            .Include(t => t.Wallet)
            .FirstOrDefaultAsync(t => t.ExternalTransactionId == externalId, cancellationToken);
    }

    public async Task<bool> ExistsByTransactionCodeAsync(string transactionCode, CancellationToken cancellationToken = default)
    {
        return await _context.WalletTransactions
            .AnyAsync(t => t.TransactionCode == transactionCode, cancellationToken);
    }

    public async Task<(IReadOnlyList<WalletTransaction> Items, int TotalCount)> GetPagedByWalletIdAsync(
        Guid walletId,
        WalletTransactionQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.WalletTransactions
            .Include(t => t.Wallet)
            .Where(t => t.WalletId == walletId);

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var clean = filter.Keyword.Trim();
            query = query.Where(t => t.TransactionCode.Contains(clean) || (t.Description != null && t.Description.Contains(clean)));
        }

        if (filter.Type.HasValue)
        {
            query = query.Where(t => t.WalletTransactionType == filter.Type.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(t => t.TransactionStatus == filter.Status.Value);
        }

        if (filter.MinAmount.HasValue)
        {
            query = query.Where(t => t.Amount >= filter.MinAmount.Value);
        }

        if (filter.MaxAmount.HasValue)
        {
            query = query.Where(t => t.Amount <= filter.MaxAmount.Value);
        }

        query = query.ApplyDateRange(t => t.CreatedAt, filter.FromDate, filter.ToDate);
        query = query.ApplySafeSorting(WalletSortMappings.TransactionMappings, filter.SortBy, filter.SortOrder, defaultSortExpression: t => t.CreatedAt, defaultSortOrder: SortOrder.Desc);

        return await query.ToPagedTupleAsync(filter.PageIndex, filter.PageSize, cancellationToken);
    }

    public async Task<IReadOnlyList<WalletTransaction>> GetListByWalletIdAsync(
        Guid walletId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.WalletTransactions
            .Where(t => t.WalletId == walletId);

        if (fromDate.HasValue)
            query = query.Where(t => t.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(t => t.CreatedAt <= toDate.Value);

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<WalletTransaction> Items, int TotalCount)> GetPagedAllAsync(
        AdminWalletTransactionsFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.WalletTransactions
            .Include(t => t.Wallet)
                .ThenInclude(w => w!.UserAccount)
            .AsQueryable();

        if (filter.UserId.HasValue)
        {
            query = query.Where(t => t.Wallet != null && t.Wallet.UserAccountId == filter.UserId.Value);
        }

        if (filter.WalletId.HasValue)
        {
            query = query.Where(t => t.WalletId == filter.WalletId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var clean = filter.Keyword.Trim();
            query = query.Where(t => t.TransactionCode.Contains(clean)
                || (t.Description != null && t.Description.Contains(clean))
                || (t.Wallet != null && t.Wallet.UserAccount != null && t.Wallet.UserAccount.UserName != null && t.Wallet.UserAccount.UserName.Contains(clean))
                || (t.Wallet != null && t.Wallet.UserAccount != null && t.Wallet.UserAccount.UserEmail != null && t.Wallet.UserAccount.UserEmail.Contains(clean)));
        }

        if (filter.Type.HasValue)
        {
            query = query.Where(t => t.WalletTransactionType == filter.Type.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(t => t.TransactionStatus == filter.Status.Value);
        }

        if (filter.MinAmount.HasValue)
        {
            query = query.Where(t => t.Amount >= filter.MinAmount.Value);
        }

        if (filter.MaxAmount.HasValue)
        {
            query = query.Where(t => t.Amount <= filter.MaxAmount.Value);
        }

        query = query.ApplyDateRange(t => t.CreatedAt, filter.FromDate, filter.ToDate);
        query = query.ApplySafeSorting(WalletSortMappings.TransactionMappings, filter.SortBy, filter.SortOrder, defaultSortExpression: t => t.CreatedAt, defaultSortOrder: SortOrder.Desc);

        return await query.ToPagedTupleAsync(filter.PageIndex, filter.PageSize, cancellationToken);
    }

    public async Task<(IReadOnlyList<WalletTransaction> Items, int TotalCount)> GetPagedWithdrawalsAsync(
        AdminWithdrawalQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.WalletTransactions
            .Include(t => t.Wallet)
                .ThenInclude(w => w!.UserAccount)
            .Where(t => t.WalletTransactionType == WalletTransactionType.WITHDRAW);

        if (filter.UserId.HasValue)
        {
            query = query.Where(t => t.Wallet != null && t.Wallet.UserAccountId == filter.UserId.Value);
        }

        if (filter.WalletId.HasValue)
        {
            query = query.Where(t => t.WalletId == filter.WalletId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var clean = filter.Keyword.Trim();
            query = query.Where(t => t.TransactionCode.Contains(clean)
                || (t.DestinationAccountNumberMasked != null && t.DestinationAccountNumberMasked.Contains(clean))
                || (t.DestinationAccountHolderName != null && t.DestinationAccountHolderName.Contains(clean))
                || (t.DestinationBankName != null && t.DestinationBankName.Contains(clean))
                || (t.Wallet != null && t.Wallet.UserAccount != null && t.Wallet.UserAccount.UserName != null && t.Wallet.UserAccount.UserName.Contains(clean)));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(t => t.TransactionStatus == filter.Status.Value);
        }

        if (filter.MinAmount.HasValue)
        {
            query = query.Where(t => t.Amount >= filter.MinAmount.Value);
        }

        if (filter.MaxAmount.HasValue)
        {
            query = query.Where(t => t.Amount <= filter.MaxAmount.Value);
        }

        query = query.ApplyDateRange(t => t.CreatedAt, filter.FromDate, filter.ToDate);
        query = query.ApplySafeSorting(WalletSortMappings.TransactionMappings, filter.SortBy, filter.SortOrder, defaultSortExpression: t => t.CreatedAt, defaultSortOrder: SortOrder.Desc);

        return await query.ToPagedTupleAsync(filter.PageIndex, filter.PageSize, cancellationToken);
    }

    public async Task<WalletTransaction?> GetActivePendingTopUpByWalletIdAsync(Guid walletId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _context.WalletTransactions
            .Include(t => t.Wallet)
            .Where(t => t.WalletId == walletId
                     && t.WalletTransactionType == WalletTransactionType.TOP_UP
                     && t.TransactionStatus == TransactionStatus.PENDING
                     && (t.ExpiredAt == null || t.ExpiredAt > now))
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> HasActivePendingWithdrawalAsync(Guid walletId, CancellationToken cancellationToken = default)
    {
        return await _context.WalletTransactions
            .AnyAsync(t => t.WalletId == walletId
                        && t.WalletTransactionType == WalletTransactionType.WITHDRAW
                        && t.TransactionStatus == TransactionStatus.PENDING,
                      cancellationToken);
    }

    public async Task AddAsync(WalletTransaction transaction, CancellationToken cancellationToken = default)
    {
        await _context.WalletTransactions.AddAsync(transaction, cancellationToken);
    }

    public void Update(WalletTransaction transaction)
    {
        _context.WalletTransactions.Update(transaction);
    }
}

