using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Financial
{
    /// <summary>
    /// Ví tiền kỹ thuật số của người dùng trên hệ thống TrustSim.
    /// Quản lý số dư khả dụng, số dư bị khóa (đặt cọc, ký quỹ), và lịch sử giao dịch.
    /// Liên kết 1-1 với <see cref="UserAccount"/>.
    /// </summary>
    public class Wallet : AuditableEntity
    {
        /// <summary>Khóa chính của ví tiền (alias cho <see cref="BaseEntity.Id"/>).</summary>
        public Guid WalletId { get => Id; private set => Id = value; }

        /// <summary>Số dư khả dụng có thể dùng để đặt giá, cọc hoặc rút tiền (VNĐ).</summary>
        public decimal AvailableBalance { get; set; } = 0;

        /// <summary>Số dư đang bị khóa tạm thời (tiền đặt cọc đấu giá, tiền giữ trong Escrow) (VNĐ).</summary>
        public decimal LockedBalance { get; set; } = 0;

        /// <summary>Trạng thái hoạt động của ví: ACTIVE (bình thường), FROZEN (đóng băng), LOCKED.</summary>
        public WalletStatus WalletStatus { get; set; } = WalletStatus.ACTIVE;

        /// <summary>
        /// Phiên bản khóa phục vụ Optimistic Concurrency Control (tránh trừ tiền đồng thời gây số dư âm).
        /// </summary>
        public int LockVersion { get; set; } = 0;

        // --- Foreign Key ---
        /// <summary>ID của tài khoản người dùng (<see cref="UserAccount"/>) sở hữu ví này (quan hệ 1-1).</summary>
        public Guid UserAccountId { get; set; }

        // --- Navigation Properties ---
        /// <summary>Tài khoản người dùng sở hữu ví này.</summary>
        public virtual UserAccount? UserAccount { get; set; }

        /// <summary>Danh sách lịch sử các giao dịch nạp / rút tiền qua ngân hàng (quan hệ 1-N).</summary>
        public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new HashSet<WalletTransaction>();

        /// <summary>Danh sách lịch sử các giao dịch tài chính nội bộ từ hoạt động đấu giá (quan hệ 1-N).</summary>
        public virtual ICollection<AuctionTransaction> AuctionTransactions { get; set; } = new HashSet<AuctionTransaction>();
    }
}