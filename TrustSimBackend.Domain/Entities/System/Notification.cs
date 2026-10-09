using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.System
{
    public class Notification: FullAuditableEntity
    {

        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Phân loại thông báo (AUCTION_WON, AUCTION_LOST, AUCTION_SELLER_CONFIRMATION_REQUIRED,...).
        /// </summary>
        public NotificationType NotificationType { get; set; } = NotificationType.SYSTEM_ALERT;

        /// <summary>
        /// Loại thực thể tham chiếu (ví dụ: "Auction", "WalletTransaction", "DisputeTicket").
        /// </summary>
        public string? ReferenceType { get; set; }

        /// <summary>
        /// ID của thực thể tham chiếu (ví dụ: AuctionId).
        /// </summary>
        public Guid? ReferenceId { get; set; }

        /// <summary>
        /// Dữ liệu bổ sung dạng JSON (ví dụ: tiền bồi thường, hạn chót, giá á quân,...).
        /// </summary>
        public string? MetadataJson { get; set; }

        /// <summary>
        /// Đường dẫn đến trang chi tiết hành động (nếu có).
        /// </summary>
        public string? ActionUrl { get; set; }

        /// <summary>
        /// Trạng thái đã đọc hay chưa đọc.
        /// </summary>
        public bool IsRead { get; set; } = false;

        /// <summary>
        /// Thời điểm người dùng đọc thông báo.
        /// </summary>
        public DateTime? ReadAt { get; set; }

        public Guid UserId { get; set; }
        public virtual UserAccount? User { get; set; }

    }
}