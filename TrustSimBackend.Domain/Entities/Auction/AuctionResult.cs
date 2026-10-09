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
        /// Nullable khi phiên không có người thắng (NO_WINNER / FAILED).
        /// </summary>
        public Guid? WinnerId { get; set; }

        /// <summary>
        /// ID của người về nhì trong phiên đấu giá (tham chiếu đến <see cref="AuctionParticipant.Id"/>).
        /// </summary>
        public Guid? RunnerUpParticipantId { get; set; }

        /// <summary>
        /// Giá đặt cao nhất của người về nhì (VNĐ).
        /// </summary>
        public decimal? RunnerUpFinalPrice { get; set; }

        /// <summary>
        /// Hạn chót thanh toán của Người thắng (Now + X giờ).
        /// </summary>
        public DateTime? PaymentDeadline { get; set; }

        /// <summary>
        /// Cờ đánh dấu đã gửi đề nghị mua cho Người về nhì khi Người thắng bùng kèo.
        /// </summary>
        public bool IsRunnerUpOffered { get; set; } = false;

        /// <summary>
        /// Hạn chót thanh toán của Người về nhì (Now + X giờ).
        /// </summary>
        public DateTime? RunnerUpPaymentDeadline { get; set; }

        /// <summary>
        /// Thời điểm hoàn tất quyết toán tài chính toàn bộ.
        /// </summary>
        public DateTime? SettledAt { get; set; }

        /// <summary>
        /// Giá cuối cùng mà người thắng phải trả (VNĐ).
        /// </summary>
        public decimal FinalPrice { get; set; }

        /// <summary>
        /// Trạng thái cuối cùng của kết quả đấu giá.
        /// </summary>
        public AuctionResultStatus AuctionResultStatus { get; set; }

        /// <summary>
        /// Thời gian xác định người chiến thắng
        /// </summary>
        public DateTime DeterminedAt { get; set; }

        /// <summary>
        /// Thời gian xác nhận người chiến thắng
        /// </summary>
        public DateTime ConfirmedAt { get; set; }

        /// <summary>
        /// Thời gian Hủy kết quả đấu giá
        /// </summary>
        public DateTime? CancelledAt { get; set; }
        /// <summary>
        /// Lý do hủy kết quả đấu giá
        /// </summary>
        public string? CancelledReason { get; set; }

        // --- CÁC TRƯỜNG MỞ RỘNG KIỂM TOÁN LIÊM CHÍNH & RUNNER-UP ---
        /// <summary>
        /// ID của Quán quân ban đầu khi gõ búa kết thúc phiên (Bất biến, không bao giờ bị ghi đè để lưu vết lịch sử nếu Quán quân bùng cọc).
        /// </summary>
        public Guid? InitialWinnerId { get; set; }

        /// <summary>
        /// Mức giá trúng đấu giá ban đầu của Quán quân.
        /// </summary>
        public decimal? InitialWinningPrice { get; set; }

        /// <summary>
        /// Nguồn xác định người chiến thắng cuối cùng (Thắng trực tiếp vs Á quân tiếp quản mua lại vs Mua ngay Hà Lan).
        /// </summary>
        public AuctionWinType WinType { get; set; } = AuctionWinType.NONE;

        /// <summary>Tỷ lệ chênh lệch giá giữa Winner và Runner-Up (FinalPrice / RunnerUpFinalPrice).</summary>
        public decimal? PriceGapRatio { get; set; }

        /// <summary>Số lượng người đặt giá độc lập (Unique Bidders) trong phiên.</summary>
        public int? UniqueBiddersCount { get; set; }

        /// <summary>Số tiền bồi thường đã chuyển vào ví Người bán từ tiền phạt cọc (50%).</summary>
        public decimal? SellerCompensationAmount { get; set; }

        /// <summary>Đánh dấu Người bán đã đồng ý (true) hoặc từ chối (false) bán cho Runner-Up.</summary>
        public bool? IsSellerApproved { get; set; }

        /// <summary>Hạn chót để Người bán đưa ra quyết định duyệt bán cho Runner-Up (24h).</summary>
        public DateTime? SellerDecisionDeadline { get; set; }

        public virtual Auction? Auction { get; set; }

        public virtual AuctionParticipant? Winner { get; set; }

        public virtual AuctionParticipant? RunnerUpParticipant { get; set; }

        public virtual AuctionParticipant? InitialWinner { get; set; }
    }
}