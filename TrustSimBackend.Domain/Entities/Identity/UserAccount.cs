using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Entities.System;

namespace TrustSimBackend.Domain.Entities.Identity
{
    /// <summary>
    /// Đại diện cho tài khoản đăng nhập của người dùng trong hệ thống TrustSim.
    /// Chứa thông tin xác thực (username, password hash) và vai trò người dùng.
    /// Liên kết 1-1 với <see cref="UserProfile"/> (thông tin cá nhân) và <see cref="UserSecurityProfile"/> (khóa ký số).
    /// </summary>
    public class UserAccount : FullAuditableEntity
    {

        /// <summary>Tên đăng nhập duy nhất của người dùng (không phân biệt hoa thường).</summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Địa chỉ email của người dùng
        /// </summary>
        public string UserEmail {get;set;}= string.Empty;

        /// <summary>Mật khẩu đã được băm (BCrypt/Argon2). Không bao giờ lưu mật khẩu thuần văn bản.</summary>
        public string PassWordHash { get; set; } = string.Empty;

        /// <summary>Vai trò của người dùng trong hệ thống: USER, ADMIN, hoặc CONSULTANT.</summary>
        public UserRole Role { get; set; } = UserRole.USER;

        // --- Navigation Properties ---
        /// <summary>Thông tin cá nhân (họ tên, CCCD, địa chỉ...) liên kết 1-1 với tài khoản này.</summary>
        public virtual UserProfile? UserProfile { get; set; }

        /// <summary>Hồ sơ bảo mật chứa cặp khóa ký số (Public Key / Encrypted Private Key) liên kết 1-1.</summary>
        public virtual UserSecurityProfile? UserSecurityProfile { get; set; }

        /// <summary>Danh sách lịch sử quyền sở hữu SIM của tài khoản này (1-N).</summary>
        public virtual ICollection<SimOwnership> SimOwnerships { get; set; } = new List<SimOwnership>();

        /// <summary>
        /// Danh sách phiên đấu giá mà tài khoản này tạo ra (người bán) (1-N).
        /// </summary>
        public virtual ICollection<Auction> Auctions { get; set; } = new List<Auction>();

        /// <summary>
        /// Danh sách các phiên đấu giá mà tài khoản này tham gia (người mua) (1-N).
        /// </summary>
        public virtual ICollection<AuctionParticipant> AuctionParticipants { get; set; } = new HashSet<AuctionParticipant>();

        /// <summary>
        /// Ví tiền của người dùng
        /// </summary>
        public virtual Wallet? Wallet { get; set; } 


        /// <summary>Danh sách các giao dịch chuyển nhượng mà tài khoản này đóng vai trò là NGƯỜI MUA (1-N).</summary>
        public virtual ICollection<TradeTransaction> BuyerTradeTransactions { get; set; } = new HashSet<TradeTransaction>();

        /// <summary>Danh sách các giao dịch chuyển nhượng mà tài khoản này đóng vai trò là NGƯỜI BÁN (1-N).</summary>
        public virtual ICollection<TradeTransaction> SellerTradeTransactions { get; set; } = new HashSet<TradeTransaction>();

        /// <summary>
        /// Danh sách các chữ ký số mà tài khoản này đã ký (1-N).
        /// </summary>
        public virtual ICollection<DigitalSignature> DigitalSignatures { get; set; } = new HashSet<DigitalSignature>();

        /// <summary>
        /// Danh sách các Refresh Token liên kết với tài khoản này (1-N).
        /// </summary>
        public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

        /// <summary>
        /// Danh sách các thông báo mà tài khoản này nhận được (1-N).
        /// </summary>
        private readonly List<Notification> _notifications = new();
        public IReadOnlyCollection<Notification> Notifications => _notifications.AsReadOnly();
    }
}
