using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Domain.Repositories;

public interface ITradeTransactionRepository
{
    // Lấy giao dịch kèm theo toàn bộ thông tin vệ tinh (Escrow, Contract, SimTransfer)
    Task<TradeTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TradeTransaction?> GetByCodeAsync(string tradeCode, CancellationToken cancellationToken = default);
    Task<TradeTransaction?> GetByAuctionIdAsync(Guid auctionId, CancellationToken cancellationToken = default);
    
    Task<(IReadOnlyList<TradeTransaction> Items, int TotalCount)> GetPagedAsync(
        AdminTradeQueryFilter filter,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<TradeTransaction> Items, int TotalCount)> GetPagedByUserIdAsync(
        Guid userId,
        MyTradeQueryFilter filter,
        CancellationToken cancellationToken = default);

    // Lấy danh sách giao dịch có hồ sơ bằng chứng sang tên chờ Admin duyệt
    Task<IReadOnlyList<TradeTransaction>> GetPendingReviewsAsync(CancellationToken cancellationToken = default);

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

    // Thêm mới một ảnh con thuộc bằng chứng chuyển nhượng SIM tường minh vào DbContext
    Task AddTransferProofImageAsync(TransferProofImage image, CancellationToken cancellationToken = default);

    // Lấy bộ bằng chứng theo ID kèm danh sách ảnh con và thông tin liên kết
    Task<TransferProof?> GetTransferProofByIdAsync(Guid transferProofId, CancellationToken cancellationToken = default);

    // Kiểm tra bằng chứng đã tạo theo IdempotencyKey trong phạm vi phiên đấu giá
    Task<TransferProof?> GetTransferProofByIdempotencyKeyAsync(Guid auctionId, string idempotencyKey, CancellationToken cancellationToken = default);

    // Kiểm tra xem phiên đấu giá có đang tồn tại bộ bằng chứng PENDING cho loại bằng chứng tương ứng không
    Task<bool> HasPendingTransferProofAsync(Guid auctionId, TransferProofType proofType, CancellationToken cancellationToken = default);

    // Cập nhật giao dịch
    void Update(TradeTransaction tradeTransaction);
    
    // Tìm các giao dịch đã quá hạn ký quỹ để hệ thống tự động hủy
    Task<IReadOnlyList<TradeTransaction>> GetExpiredEscrowTransactionsAsync(DateTime currentTime);
}
