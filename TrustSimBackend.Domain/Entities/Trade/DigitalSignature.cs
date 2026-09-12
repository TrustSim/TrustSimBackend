using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Trade
{
    /// <summary>
    /// Đại diện cho một chữ ký số dùng trong giao dịch chuyển nhượng SIM.
    /// Lưu trữ thông tin về loại chữ ký số (hợp đồng, biên bản), dữ liệu được ký,
    /// và trạng thái xác thực.
    /// </summary>
    public class DigitalSignature: BaseEntity
    {

        /// <summary>
        /// Loại chữ ký số (hợp đồng, biên bản).
        /// </summary>
        public SignatureRole SignatureRole{get;set;}

        /// <summary>
        /// Khóa công khai của người ký.
        /// </summary>
        public string PublicKey {get;set;}=string.Empty;

        /// <summary>
        /// Chữ ký số.
        /// </summary>
        public string CryptographicSignature {get;set;}= string.Empty;

        /// <summary>
        /// Thời gian ký số.
        /// </summary>
        public DateTime SignedAt {get;set;}= DateTime.UtcNow;

        /// <summary>
        /// Khóa ngoại tới <see cref="TransferContract"/>.
        /// </summary>
        public Guid ContracId{get;set;}

        /// <summary>
        /// Hợp đồng liên quan.
        /// </summary>
        public virtual TransferContract? Contract { get; set; }

        /// <summary>
        /// Khóa ngoại tới <see cref="UserAccount"/>.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Người dùng đã ký.
        /// </summary>
        public virtual UserAccount? User { get; set; }

    }
}