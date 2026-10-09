namespace TrustSimBackend.Domain.Enums
{
    /// <summary>
    /// Định nghĩa nguồn xác định người chiến thắng cuối cùng của phiên đấu giá
    /// (Thắng trực tiếp vs Á quân tiếp quản mua lại vs Mua ngay phiên Hà Lan).
    /// </summary>
    public enum AuctionWinType
    {
        /// <summary>
        /// Chưa xác định / Phiên thất bại hoặc bị hủy (không có người mua).
        /// </summary>
        NONE = 0,

        /// <summary>
        /// Quán quân thắng trực tiếp khi gõ búa kết thúc phiên và hoàn tất thanh toán.
        /// </summary>
        ORIGINAL_WINNER = 1,

        /// <summary>
        /// Á quân tiếp quản nhận quyền mua SIM sau khi Quán quân ban đầu bùng cọc / quá hạn thanh toán.
        /// </summary>
        RUNNER_UP_TAKEOVER = 2,

        /// <summary>
        /// Người tham gia bấm MUA NGAY trong phiên đấu giá giảm dần (Dutch Auction).
        /// </summary>
        DUTCH_BUY_NOW = 3
    }
}
