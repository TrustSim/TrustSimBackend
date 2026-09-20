using Microsoft.AspNetCore.Identity;
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
    /// Đại diện cho tài khoản đăng nhập của người dùng trong hệ thống TrustSim kế thừa từ ASP.NET Core Identity.
    /// Chứa thông tin xác thực, bảo mật, vai trò và quản lý phiên.
    /// Liên kết 1-1 với <see cref="UserProfile"/> (thông tin cá nhân) và <see cref="UserSecurityProfile"/> (khóa ký số).
    /// </summary>
    public class UserAccount : IdentityUser<Guid>, IAuditlog, ISoftDelete
    {
        /// <summary>Trạng thái hoạt động của tài khoản (true = hoạt động, false = bị khóa/vô hiệu hóa).</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Vai trò của người dùng trong hệ thống: USER, ADMIN, hoặc CONSULTANT.</summary>
        public UserRole Role { get; set; } = UserRole.USER;

        /// <summary>Thuộc tính ánh xạ tương thích ngược với UserEmail.</summary>
        public string UserEmail 
        { 
            get => Email ?? string.Empty; 
            set => Email = value; 
        }

        /// <summary>Thuộc tính ánh xạ tương thích ngược với PassWordHash.</summary>
        public string PassWordHash 
        { 
            get => PasswordHash ?? string.Empty; 
            set => PasswordHash = value; 
        }

        // --- Audit & Soft Delete Implementation ---
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public DateTime UpdatedDate { get; set; }
        public string? UpdatedBy { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedDate { get; set; }
        public string? DeletedBy { get; set; }

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
