using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

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
        WalletTransactionType? type,
        TransactionStatus? status,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.WalletTransactions
            .Include(t => t.Wallet)
            .Where(t => t.WalletId == walletId);

        if (type.HasValue)
        {
            query = query.Where(t => t.WalletTransactionType == type.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(t => t.TransactionStatus == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<WalletTransaction> Items, int TotalCount)> GetPagedAllAsync(
        WalletTransactionType? type,
        TransactionStatus? status,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.WalletTransactions
            .Include(t => t.Wallet)
            .AsQueryable();

        if (type.HasValue)
        {
            query = query.Where(t => t.WalletTransactionType == type.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(t => t.TransactionStatus == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
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

