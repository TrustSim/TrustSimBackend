using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Trade
{
    /// <summary>
    /// Hợp đồng chuyển nhượng số hóa trên hệ thống.
    /// </summary>
    public class TransferContract: AuditableEntity
    {
        /// <summary>
        /// ID của hợp đồng chuyển nhượng.
        /// </summary>
        public Guid TransferContractId {get=> Id;private set => Id = value;}

        /// <summary>
        /// Đường dẫn tới tài liệu hợp đồng.
        /// </summary>
        public string DocumentUrl {get;set;}= string.Empty;
        
        /// <summary>
        /// Mã hash của tài liệu hợp đồng (dùng để xác minh tính toàn vẹn).
        /// </summary>
        public string DocumentHash {get;set;}= string.Empty;
        
        /// <summary>
        /// Mã giao dịch trên blockchain.
        /// </summary>
        public string BlockchainTxHash {get;set;}= string.Empty;

        /// <summary>
        /// Trạng thái của hợp đồng.
        /// </summary>
        public ContractStatus ContractStatus {get;set;}

        /// <summary>
        /// Khóa ngoại tới <see cref="TradeTransaction"/>.
        /// </summary>
        public Guid TradeTransactionId {get;set;}

        /// <summary>
        /// Giao dịch liên quan.
        /// </summary>
        public virtual TradeTransaction? TradeTransaction { get; set; } 

        


    }
}