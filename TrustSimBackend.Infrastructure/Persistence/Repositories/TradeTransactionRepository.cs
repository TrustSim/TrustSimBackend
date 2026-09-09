using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

public class TradeTransactionRepository : ITradeTransactionRepository
{
    private readonly TrustSimDbContext _dbContext;

    public TradeTransactionRepository(TrustSimDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(TradeTransaction tradeTransaction, CancellationToken cancellationToken = default)
    {
        await _dbContext.TradeTransactions.AddAsync(tradeTransaction, cancellationToken);
    }

    public async Task<TradeTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TradeTransactions
            .Include(t => t.Buyer)
                .ThenInclude(u => u!.UserProfile)
            .Include(t => t.Seller)
                .ThenInclude(u => u!.UserProfile)
            .Include(t => t.SimNumber)
            .Include(t => t.Escrow)
            .Include(t => t.TransferContract)
            .Include(t => t.SimTransfer)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<TradeTransaction> Items, int TotalCount)> GetPagedAsync(
        TradeTransactionStatus? status,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.TradeTransactions
            .Include(t => t.Buyer)
                .ThenInclude(u => u!.UserProfile)
            .Include(t => t.Seller)
                .ThenInclude(u => u!.UserProfile)
            .Include(t => t.SimNumber)
            .Include(t => t.Escrow)
            .Include(t => t.TransferContract)
            .Include(t => t.SimTransfer)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(t => t.TradeTransactionStatus == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.CreatedDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<TradeTransaction> Items, int TotalCount)> GetPagedByUserIdAsync(
        Guid userId,
        TradeTransactionStatus? status,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.TradeTransactions
            .Include(t => t.Buyer)
                .ThenInclude(u => u!.UserProfile)
            .Include(t => t.Seller)
                .ThenInclude(u => u!.UserProfile)
            .Include(t => t.SimNumber)
            .Include(t => t.Escrow)
            .Where(t => t.BuyerId == userId || t.SellerId == userId);

        if (status.HasValue)
        {
            query = query.Where(t => t.TradeTransactionStatus == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.CreatedDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public void Update(TradeTransaction tradeTransaction)
    {
        _dbContext.TradeTransactions.Update(tradeTransaction);
    }

    public async Task<IReadOnlyList<TradeTransaction>> GetExpiredEscrowTransactionsAsync(DateTime currentTime)
    {
        return await _dbContext.TradeTransactions
            .Include(t => t.Escrow)
            .Where(t => t.TradeTransactionStatus == TradeTransactionStatus.PendingEscrow 
                     && t.EscrowDeadline < currentTime)
            .ToListAsync();
    }
}