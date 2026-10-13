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
    
    // Thêm mới một hợp đồng chuyển nhượng tường minh vào DbContext
    Task AddTransferContractAsync(TransferContract contract, CancellationToken cancellationToken = default);

    // Thêm mới một chữ ký số tường minh vào DbContext
    Task AddDigitalSignatureAsync(DigitalSignature signature, CancellationToken cancellationToken = default);

    // Thêm mới một tiến trình bàn giao SIM tường minh vào DbContext
    Task AddSimTransferAsync(SimTransfer simTransfer, CancellationToken cancellationToken = default);

    // Thêm mới một bằng chứng chuyển nhượng SIM tường minh vào DbContext
    Task AddTransferProofAsync(TransferProof transferProof, CancellationToken cancellationToken = default);

    // Cập nhật giao dịch
    void Update(TradeTransaction tradeTransaction);
    
    // Tìm các giao dịch đã quá hạn ký quỹ để hệ thống tự động hủy
    Task<IReadOnlyList<TradeTransaction>> GetExpiredEscrowTransactionsAsync(DateTime currentTime);
}