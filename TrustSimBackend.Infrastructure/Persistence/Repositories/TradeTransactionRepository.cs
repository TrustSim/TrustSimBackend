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
        // Khi lấy Giao dịch, lấy luôn các bảng phụ liên quan để xử lý nghiệp vụ
        return await _dbContext.TradeTransactions
            .Include(t => t.Escrow)
            .Include(t => t.TransferContract)
            .Include(t => t.SimTransfer)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
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