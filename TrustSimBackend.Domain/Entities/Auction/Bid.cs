using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Auctions
{
    public class Bid : AuditableEntity
    {

        /// <summary>
        /// ID của phiên đấu giá (FK đến <see cref="Auction.Id"/>).
        /// </summary>
        public Guid AuctionId { get; set; }

        /// <summary>
        /// Tài khoản người dùng đặt giá (FK đến <see cref="AuctionParticipant.Id"/>).
        /// </summary>
        public Guid ParticipantId { get; set; }


        /// <summary>
        /// Số tiền đặt giá (đơn vị: VND).
        /// </summary>
        public decimal BidAmount { get; set; }

        
        /// <summary>
        /// Trạng thái của giá:
        /// <see cref="BidStatus.Pending"/> - Đang chờ xử lý (mặc định).
        /// <see cref="BidStatus.Accepted"/> - Giá được chấp nhận (người trả giá có thể bị hạn chế số lượng bid).
        /// <see cref="BidStatus.Rejected"/> - Giá bị từ chối bởi người bán (kèm lý do).
        /// <see cref="BidStatus.Withdraw"/> - Giá bị người trả giá rút lại.
        /// </summary>
        public BidStatus Status { get; set; } = BidStatus.ACCEPTED;

        /// <summary>
        /// Thời gian đặt giá
        /// </summary>
        public DateTime PlacedAt {get;set;}

        /// <summary>
        /// Thời gian bị từ chối
        /// </summary>
        public DateTime? RejectedAt {get;set;}

        /// <summary>
        /// Lý do bị từ chối
        /// </summary>
        public string? RejectedReason {get;set;}


        /// <summary>
        /// ID của phiên đấu giá mà người tham gia này thuộc về.
        /// </summary>
        public virtual Auction? Auction { get; set; }
        /// <summary>
        /// Tài khoản người dùng tham gia đấu giá.
        /// </summary>
        public virtual AuctionParticipant? Participant { get; set; }
    }
}