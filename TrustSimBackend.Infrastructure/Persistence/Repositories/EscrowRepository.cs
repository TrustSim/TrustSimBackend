using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

public class EscrowRepository : IEscrowRepository
{
    private readonly TrustSimDbContext _context;

    public EscrowRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<Escrow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Escrows.Include(e => e.TradeTransaction).FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<Escrow?> GetByTradeTransactionIdAsync(Guid tradeTransactionId, CancellationToken cancellationToken = default)
        => await _context.Escrows.FirstOrDefaultAsync(e => e.TradeTransactionId == tradeTransactionId, cancellationToken);

    public async Task AddAsync(Escrow escrow, CancellationToken cancellationToken = default)
        => await _context.Escrows.AddAsync(escrow, cancellationToken);

    public void Update(Escrow escrow)
        => _context.Escrows.Update(escrow);
}
