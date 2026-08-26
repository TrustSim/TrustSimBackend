namespace TrustSimBackend.Domain.Enums;

public enum TradeTransactionStatus
{
    // Luồng khởi tạo
    PendingEscrow = 1,        // Chờ người mua thanh toán ký quỹ (nằm trong escrowDeadline)
    
    // Luồng xử lý
    PendingSignature = 2,     // Đã ký quỹ xong -> Chờ 2 bên ký hợp đồng số (nằm trong signingDeadline)
    PendingTransfer = 3,      // Đã ký xong -> Chờ người bán sang tên SIM & tải bằng chứng (nằm trong transferDeadline)
    
    // Luồng thành công
    Completed = 4,            // Giao dịch hoàn tất -> Tiến hành giải ngân (SellerPayout)
    
    // Luồng thất bại / Hủy (Cần xử lý Refund)
    Cancelled_EscrowTimeout = 5,   // Hủy do người mua không đóng tiền ký quỹ đúng hạn
    Cancelled_SigningTimeout = 6,  // Hủy do 1 trong 2 bên không ký hợp đồng đúng hạn
    Cancelled_TransferTimeout = 7, // Hủy do người bán không sang tên SIM đúng hạn
    Cancelled_ByAdmin = 8,         // Hủy do Admin can thiệp (ví dụ: phát hiện gian lận)
    
    // Luồng tranh chấp (Mở rộng thêm nếu cần)
    Disputed = 9                   // Đang xảy ra tranh chấp (ví dụ: người mua báo chưa nhận được SIM)
}   