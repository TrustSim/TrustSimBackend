using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Auctions
{
    /// <summary>
    /// Đại diện cho một người tham gia đấu giá (bidder) trong một phiên đấu giá cụ thể.
    /// Lưu trữ thông tin về số tiền đặt cược (bid amount), trạng thái xác thực và lịch sử tham gia.
    /// </summary>
    public class AuctionParticipant : FullAuditableEntity
    {

        /// <summary>
        /// ID của phiên đấu giá mà người tham gia này thuộc về (FK đến <see cref="Auction.Id"/>).
        /// </summary>
        public Guid AuctionId { get; set; }

        /// <summary>
        /// ID của người tham gia đấu giá (tài khoản người dùng) (FK đến <see cref="UserAccount.Id"/>).
        /// </summary>
        public Guid UserAccountId { get; set; }


        /// <summary>
        /// Trạng thái tham gia của người dùng trong phiên đấu giá.
        /// Mặc định là <see cref="AuctionParticipantStatus.Pending"/>.
        /// </summary>
        public ParticipantStatus Status { get; set; }

        /// <summary>
        /// Số lượng giá thầu được chấp nhận của người dùng.
        /// </summary>
        public int AcceptedBidCount { get; set; }

        /// <summary>
        /// Số lượng giá thầu bị từ chối của người dùng.
        /// </summary>
        public int RejectedBidCount { get; set; }

        /// <summary>
        /// Ngày tham gia đấu giá.
        /// </summary>
        public DateTime JoinedAt {get; set;}

        /// <summary>
        /// Ngày từ chối đấu giá.
        /// </summary>
        public DateTime? RejectedAt {get;set;}

        /// <summary>
        /// Lý do từ chối đấu giá.
        /// </summary>
        public string? RejectReason {get;set;}

        /// <summary>
        /// Thời gian bị khóa 
        /// </summary>
        public DateTime? BlockedAt {get;set;}

        /// <summary>
        /// Lý do bị khóa
        /// </summary>
        public string? BlockedReason {get;set;}

        /// <summary>
        /// Số lần người chơi này đã kích hoạt bù giờ phút chót trong phiên (so sánh với K).
        /// </summary>
        public int SnipeExtensionCount { get; set; } = 0;

        /// <summary>
        /// Cờ đánh dấu người dùng đang trực tiếp online trong phòng đấu giá.
        /// </summary>
        public bool IsInRoom { get; set; } = false;

        /// <summary>
        /// Phiên đấu giá mà người tham gia này thuộc về.
        /// </summary>
        public virtual Auction? Auction { get; set; }
        /// <summary>
        /// Tài khoản người dùng tham gia đấu giá.
        /// </summary>
        public virtual UserAccount? UserAccount { get; set; }

        /// <summary>
        /// Danh sách các giá đã đặt trong phiên đấu giá này (quan hệ 1-n).
        /// </summary>
        public virtual ICollection<Bid> Bids { get; set; } = new HashSet<Bid>();

        /// <summary>
        /// Lịch sử đánh giá rủi ro (quan hệ 1-n)
        /// </summary>
        public virtual ICollection<FraudAssessment> FraudAssessments { get; set; } = new HashSet<FraudAssessment>();

        /// <summary>
        /// Danh sách các khoản ký quỹ đã đặt trong phiên đấu giá này (quan hệ 1-n).
        /// </summary>
        public virtual ICollection<AuctionDeposit> Deposits { get; set; } = new HashSet<AuctionDeposit>();


    }
}