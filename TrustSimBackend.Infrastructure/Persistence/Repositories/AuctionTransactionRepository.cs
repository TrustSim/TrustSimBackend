using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

public class AuctionTransactionRepository : IAuctionTransactionRepository
{
    private readonly TrustSimDbContext _context;

    public AuctionTransactionRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<AuctionTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.AuctionTransactions.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AuctionTransaction>> GetByAuctionIdAsync(Guid auctionId, CancellationToken cancellationToken = default)
        => await _context.AuctionTransactions.Where(t => t.AuctionId == auctionId).OrderByDescending(t => t.CreatedAt).ToListAsync(cancellationToken);

    public async Task AddAsync(AuctionTransaction transaction, CancellationToken cancellationToken = default)
        => await _context.AuctionTransactions.AddAsync(transaction, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<AuctionTransaction> transactions, CancellationToken cancellationToken = default)
        => await _context.AuctionTransactions.AddRangeAsync(transactions, cancellationToken);
}
