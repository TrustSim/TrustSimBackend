namespace TrustSimBackend.Domain.Enums
{
    public enum AuctionResultStatus
    {
        PENDING,            // Đang chờ xử lý kết quả ban đầu
        PENDING_PAYMENT,    // Chờ người thắng hoàn tất thanh toán (trong X giờ)
        CONFIRMED,          // Đã thanh toán thành công, chờ chuyển nhượng SIM
        RUNNER_UP_PENDING,  // Người thắng bùng cọc -> Đang chờ Người về nhì thanh toán (trong X giờ)
        SETTLED,            // Đã hoàn tất quyết toán tài chính toàn bộ
        CANCELLED,          // Bị hủy bởi Admin do gian lận
        NO_WINNER,          // Không có người tham gia đặt giá / Chạm sàn Dutch không ai mua
        FAILED,             // Thất bại hoàn toàn (Người thắng bùng kèo & Người về nhì từ chối/bùng kèo)
        INVALIDATED_SHIELDING, // Bị hủy do phát hiện dấu hiệu Lá chắn giá / Thổi giá bất thường
        PENDING_SELLER_CONFIRMATION // Đang chờ Người bán duyệt bán cho Á quân kèm nhận tiền bồi thường
    }
}