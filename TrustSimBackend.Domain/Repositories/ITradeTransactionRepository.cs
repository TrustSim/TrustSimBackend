using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Repositories;

public interface ITradeTransactionRepository
{
    // Lấy giao dịch kèm theo toàn bộ thông tin vệ tinh (Escrow, Contract, SimTransfer)
    Task<TradeTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<(IReadOnlyList<TradeTransaction> Items, int TotalCount)> GetPagedAsync(
        TradeTransactionStatus? status,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<TradeTransaction> Items, int TotalCount)> GetPagedByUserIdAsync(
        Guid userId,
        TradeTransactionStatus? status,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);

    // Thêm mới một giao dịch vào DB
    Task AddAsync(TradeTransaction tradeTransaction, CancellationToken cancellationToken = default);
    
    // Cập nhật giao dịch
    void Update(TradeTransaction tradeTransaction);
    
    // Tìm các giao dịch đã quá hạn ký quỹ để hệ thống tự động hủy
    Task<IReadOnlyList<TradeTransaction>> GetExpiredEscrowTransactionsAsync(DateTime currentTime);
}