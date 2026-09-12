using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Auctions
{
    /// <summary>
    /// Đại diện cho một phiên đấu giá SIM trên hệ thống TrustSim.
    /// Mỗi phiên đấu giá gắn với một số SIM duy nhất và được tạo bởi một người dùng (Author).
    /// </summary>
    public class Auction : FullAuditableEntity
    {

        /// <summary>Mã định danh duy nhất của phiên đấu giá (VD: "AUC-20240801-001"), dùng để hiển thị cho người dùng.</summary>
        public string AuctionCode { get; set; } = string.Empty;

        /// <summary>Loại hình đấu giá: English (giá tăng dần) hoặc Dutch (giá giảm dần).</summary>
        public AuctionType AuctionType { get; set; }

        /// <summary>Trạng thái hiện tại của phiên đấu giá: DRAFT → SCHEDULED → ACTIVE → ENDED / CANCELLED.</summary>
        public AuctionStatus AuctionStatus { get; set; } = AuctionStatus.DRAFT;

        /// <summary>Giá khởi điểm của phiên đấu giá (VNĐ).</summary>
        public decimal StartingPrice { get; set; }

        /// <summary>Giá hiện tại (giá cao nhất đang được chấp nhận). Được cập nhật sau mỗi lượt đặt giá hợp lệ.</summary>
        public decimal CurrentPrice { get; set; }

        /// <summary>Giá sàn bí mật (reserve price). Phiên đấu giá chỉ thành công nếu giá thắng vượt qua mức này.</summary>
        public decimal ReservePrice { get; set; }

        /// <summary>Thời điểm phiên đấu giá bắt đầu nhận lượt đặt giá.</summary>
        public DateTime StartAt { get; set; }

        /// <summary>Thời điểm phiên đấu giá kết thúc (có thể được gia hạn bởi cơ chế Anti-Sniping).</summary>
        public DateTime EndAt { get; set; }

        /// <summary>
        /// Phiên bản khóa để xử lý Optimistic Concurrency (tránh race condition khi nhiều người đặt giá đồng thời).
        /// Tăng lên 1 sau mỗi lần cập nhật thành công.
        /// </summary>
        public int LockVersion { get; set; } = 1;

        /// <summary>Thời điểm mở đăng ký tham gia đấu giá (nộp cọc).</summary>
        public DateTime RegistrationStartAt { get; set; }

        /// <summary>Thời điểm đóng đăng ký tham gia đấu giá.</summary>
        public DateTime RegistrationEndAt { get; set; }

        // --- Foreign Keys ---
        /// <summary>ID của người dùng tạo ra phiên đấu giá này (người bán).</summary>
        public Guid AuthorId { get; set; }

        /// <summary>ID của số SIM được đưa ra đấu giá.</summary>
        public Guid SimNumberId { get; set; }

        // --- Navigation Properties ---
        /// <summary>Người dùng tạo phiên đấu giá (người bán).</summary>
        public virtual UserAccount? Author { get; set; }

        /// <summary>Số SIM được đưa ra đấu giá trong phiên này.</summary>
        public virtual SimNumber? SimNumber { get; set; }

        /// <summary>Bộ quy tắc chi tiết của phiên đấu giá (quan hệ 1-1).</summary>
        public virtual AuctionRule? AuctionRule { get; set; }

        /// <summary>
        /// Danh sách những người tham gia đấu giá (quan hệ 1-n).
        /// </summary>
        public virtual ICollection<AuctionParticipant> Participants { get; set; } = new HashSet<AuctionParticipant>();

        /// <summary>
        /// Danh sách các giá đã đặt trong phiên đấu giá này (quan hệ 1-n).
        /// </summary>
        public virtual ICollection<Bid> Bids { get; set; } = new HashSet<Bid>();

        /// <summary>
        /// Giao dịch liên quan đến phiên đấu giá này.
        /// </summary>
        public virtual TradeTransaction? TradeTransaction { get; set; }
    }
}