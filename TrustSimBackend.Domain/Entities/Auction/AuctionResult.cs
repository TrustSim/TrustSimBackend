using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Auctions
{
    /// <summary>
    /// Lưu trữ kết quả cuối cùng của một phiên đấu giá.
    /// Được tạo ngay khi phiên đấu giá kết thúc (dù là thành công, thất bại hay bị hủy).
    /// Không thể xóa — dùng để lưu vết pháp lý sau khi phiên kết thúc.
    /// </summary>
    public class AuctionResult: AuditableEntity
    {

        /// <summary>
        /// ID của phiên đấu giá mà kết quả này thuộc về.
        /// </summary>
        public Guid AuctionId {get;set;}

        /// <summary>
        /// ID của người thắng đấu giá (tham chiếu đến <see cref="AuctionParticipant.Id"/>).
        /// </summary>
        public Guid WinnerId {get;set;}

        /// <summary>
        /// Giá cuối cùng mà người thắng phải trả (VNĐ).
        /// </summary>
        public decimal FinalPrice {get;set;}

        /// <summary>
        /// Trạng thái cuối cùng của kết quả đấu giá.
        /// </summary>
        public AuctionResultStatus AuctionResultStatus {get;set;}

        /// <summary>
        /// Thời gian xác định người chiến thắng
        /// </summary>
        public DateTime DeterminedAt {get;set;}

        /// <summary>
        /// Thời gian xác nhân người chiến thắng
        /// </summary>
        public DateTime ConfirmedAt {get;set;}

        /// <summary>
        /// Thời gian Hủy kết quả đấu giá
        /// </summary>
        public DateTime? CancelledAt {get;set;}
        /// <summary>
        /// Lý do hủy kết quả đấu giá
        /// </summary>
        public string? CancelledReason {get;set;}

        public virtual Auction? Auction { get; set; }

        public virtual AuctionParticipant? Winner { get; set; }
    }
}