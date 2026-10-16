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
        /// Đường dẫn tới tài liệu hợp đồng PDF đã ký số niêm phong (null khi đang DRAFT chờ ký).
        /// </summary>
        public string? DocumentUrl { get; set; }
        
        /// <summary>
        /// Mã hash SHA-256 của tài liệu hợp đồng đã niêm phong (null khi đang DRAFT chờ ký).
        /// </summary>
        public string? DocumentHash { get; set; }

        /// <summary>
        /// [Văn bản 1] Đường dẫn tới Phiếu xác nhận kết quả đấu giá / Phiếu chốt giá trúng điện tử (PDF Supabase).
        /// </summary>
        public string? WinningNoticeUrl { get; set; }

        /// <summary>
        /// [Văn bản 1] Mã hash SHA-256 của Phiếu chốt giá trúng điện tử.
        /// </summary>
        public string? WinningNoticeHash { get; set; }

        /// <summary>
        /// [Văn bản 3] Đường dẫn tới Biên bản nghiệm thu & thanh lý hợp đồng chuyển nhượng SIM (PDF Supabase).
        /// </summary>
        public string? LiquidationDocumentUrl { get; set; }

        /// <summary>
        /// [Văn bản 3] Mã hash SHA-256 của Biên bản nghiệm thu & thanh lý đã niêm phong.
        /// </summary>
        public string? LiquidationDocumentHash { get; set; }

        /// <summary>
        /// [Văn bản 3] Thời điểm niêm phong chữ ký số Biên bản nghiệm thu & thanh lý.
        /// </summary>
        public DateTime? LiquidationSealedAt { get; set; }
        
        /// <summary>
        /// Mã giao dịch trên blockchain.
        /// </summary>
        public string BlockchainTxHash {get;set;}= string.Empty;

        /// <summary>
        /// Trạng thái của hợp đồng.
        /// </summary>
        public ContractStatus ContractStatus {get;set;} = ContractStatus.DRAFT;

        /// <summary>
        /// Số Serial của Chứng thư số X.509 mà Sàn đã dùng để ký PAdES.
        /// </summary>
        public string? PadesSignatureSerial { get; set; }

        /// <summary>
        /// Mã chữ ký số mật mã PAdES của Sàn.
        /// </summary>
        public string? PadesSignatureHash { get; set; }

        /// <summary>
        /// Mốc thời gian độc lập nhận từ máy chủ RFC 3161 Timestamp Authority (TSA).
        /// </summary>
        public DateTime? TsaTimestamp { get; set; }

        /// <summary>
        /// Tên máy chủ TSA đã cấp chứng thực thời gian.
        /// </summary>
        public string? TsaAuthority { get; set; }

        /// <summary>
        /// Đánh dấu hợp đồng được niêm phong thông qua Cơ chế Ủy quyền cưỡng chế (do Bên Bán quá hạn 24h).
        /// </summary>
        public bool IsAutoEnforced { get; set; } = false;

        /// <summary>
        /// Thời điểm hoàn tất ký số PAdES niêm phong tài liệu.
        /// </summary>
        public DateTime? SealedAt { get; set; }

        /// <summary>
        /// Khóa ngoại tới <see cref="TradeTransaction"/>.
        /// </summary>
        public Guid TradeTransactionId {get;set;}

        /// <summary>
        /// Giao dịch liên quan.
        /// </summary>
        public virtual TradeTransaction? TradeTransaction { get; set; } 

        /// <summary>
        /// Danh sách các dấu vết chữ ký điện tử của Người Mua và Người Bán (quan hệ 1-N).
        /// </summary>
        public virtual ICollection<DigitalSignature> DigitalSignatures { get; set; } = new HashSet<DigitalSignature>();
    }
}