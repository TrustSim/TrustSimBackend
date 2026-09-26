using TrustSimBackend.Domain.Base;

namespace TrustSimBackend.Domain.Entities.Auctions
{
    /// <summary>
    /// Lưu trữ toàn bộ cấu hình quy tắc chi tiết của một phiên đấu giá (quan hệ 1-1 với <see cref="Auction"/>).
    /// Tách riêng khỏi Auction để giảm kích thước bảng chính và nhóm các tham số cấu hình liên quan lại với nhau.
    /// Không được phép xóa — quy tắc cần lưu vết pháp lý sau khi phiên kết thúc.
    /// </summary>
    public class AuctionRule : AuditableEntity
    {

        // --- Quy tắc đặt giá ---
        /// <summary>Mức tăng giá tối thiểu cho mỗi lượt đặt giá (VNĐ). VD: 100,000đ.</summary>
        public decimal MinimumBidIncrement { get; set; }

        /// <summary>Số tiền đặt cọc bắt buộc để đăng ký tham gia đấu giá (VNĐ).</summary>
        public decimal DepositAmount { get; set; }

        /// <summary>Số lượt đặt giá tối đa mà một người tham gia được phép thực hiện trong toàn bộ phiên.</summary>
        public int MaximumBidCountPerParticipant { get; set; }

        /// <summary>Số lượt đặt giá tối đa trong vòng 1 phút (rate limiting để chống spam).</summary>
        public int MaximumBidCountPerMinute { get; set; }

        /// <summary>Khoảng thời gian tối thiểu (giây) giữa 2 lượt đặt giá liên tiếp của cùng một người.</summary>
        public float MinimumBidIntervalSeconds { get; set; }

        /// <summary>Hệ số nhân N: Giới hạn mức tăng tối đa trong 1 lần đặt giá (Kiểu Anh). BidAmount &lt;= CurrentPrice + N * MinimumBidIncrement.</summary>
        public int MaxStepMultiplier { get; set; } = 10;

        /// <summary>Giới hạn K lần: Số lần tối đa một người chơi được phép kích hoạt bù giờ phút chót (Kiểu Anh).</summary>
        public int MaxSnipeExtensionsPerParticipant { get; set; } = 3;

        /// <summary>Thời hạn X giờ: Thời gian cho phép người thắng / người về nhì hoàn tất thanh toán trước khi bị phạt cọc.</summary>
        public int PaymentGracePeriodHours { get; set; } = 24;

        // --- Cấu hình Anti-Sniping ---
        /// <summary>
        /// Ngưỡng thời gian (giây) trước khi phiên kết thúc: nếu có bid mới trong khoảng này,
        /// phiên sẽ được tự động gia hạn thêm <see cref="ExtensionDurationSeconds"/> giây.
        /// Ngăn chặn hành vi đặt giá thắng vào giây cuối (sniping).
        /// </summary>
        public float ExtensionThresholdSeconds { get; set; }

        /// <summary>Số giây được gia hạn thêm mỗi lần phát hiện bid trong ngưỡng Anti-Sniping.</summary>
        public float ExtensionDurationSeconds { get; set; }

        /// <summary>Số lần tối đa phiên đấu giá được phép gia hạn (tránh kéo dài vô hạn).</summary>
        public int MaximumExtensionCount { get; set; }

        // --- Cấu hình Dutch Auction ---
        /// <summary>
        /// Mức giảm giá tự động sau mỗi chu kỳ (VNĐ) — chỉ áp dụng với Dutch Auction.
        /// Giá sẽ giảm dần từ <c>StartingPrice</c> đến <c>ReservePrice</c>.
        /// </summary>
        public decimal DutchDecreaseAmount { get; set; }

        /// <summary>Khoảng thời gian (giây) giữa các lần giảm giá tự động trong Dutch Auction.</summary>
        public float DutchDecreaseIntervalSeconds { get; set; }

        // --- Foreign Key ---
        /// <summary>ID của phiên đấu giá mà bộ quy tắc này thuộc về.</summary>
        public Guid AuctionId { get; set; }

        // --- Navigation Property ---
        /// <summary>Phiên đấu giá liên kết (quan hệ 1-1).</summary>
        public virtual Auction? Auction { get; set; }
    }
}