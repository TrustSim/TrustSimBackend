using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Trade
{
    /// <summary>
    /// Đại diện cho một giao dịch chuyển nhượng SIM giữa người mua và người bán.
    /// </summary>
    public class SimTransfer: AuditableEntity
    {
        /// <summary>
        /// Khóa chính của giao dịch (alias cho <see cref="BaseEntity.Id"/>).
        /// </summary>
        public Guid SimTransferId {get=> Id; private set =>Id=value;}

        /// <summary>
        /// Thời gian bắt đầu chuyển nhượng.
        /// </summary>
        public DateTime StartAt {get;set;}= DateTime.UtcNow;

        /// <summary>
        /// Thời gian hết hạn chuyển nhượng.
        /// </summary>
        public DateTime Deadline {get;set;}= DateTime.UtcNow;

        /// <summary>
        /// Trạng thái hiện tại của giao dịch chuyển nhượng.
        /// </summary>
        public SimTransferStatus SimTransferStatus {get;set;}= SimTransferStatus.PENDING_SELLER_ACTION;

        /// <summary>
        /// Lý do hủy giao dịch chuyển nhượng.
        /// </summary>
        public string? CancelledReason {get;set;}

        /// <summary>
        /// Khóa ngoại liên kết đến giao dịch chuyển nhượng.
        /// </summary>
        public Guid TradeTransactionId {get;set;}= Guid.Empty;

        /// <summary>
        /// Giao dịch chuyển nhượng liên quan.
        /// </summary>
        public virtual TradeTransaction TradeTransaction {get;set;}= new();

        private readonly List<TransferProof> _transferProofs = new();
        public IReadOnlyCollection<TransferProof> TransferProofs => _transferProofs.AsReadOnly();
        
    }
}