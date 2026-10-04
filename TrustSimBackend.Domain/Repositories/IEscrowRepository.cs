using TrustSimBackend.Domain.Entities.Financial;

namespace TrustSimBackend.Domain.Repositories;

public interface IEscrowRepository
{
    Task<Escrow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Escrow?> GetByTradeTransactionIdAsync(Guid tradeTransactionId, CancellationToken cancellationToken = default);
    Task AddAsync(Escrow escrow, CancellationToken cancellationToken = default);
    void Update(Escrow escrow);
}
