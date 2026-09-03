namespace TrustSimBackend.Application.Features.Financial.DTOs;

/// <summary>
/// DTO trả về cho người dùng khi tạo yêu cầu nạp tiền (kèm thông tin thanh toán & VietQR SePay).
/// </summary>
public class DepositResponseDto
{
    public Guid TransactionId { get; set; }
    public string TransactionCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public string TransferContent { get; set; } = string.Empty;
    public string QrCodeUrl { get; set; } = string.Empty;
    public DateTime ExpiredAt { get; set; }
    public string Instructions { get; set; } = "Vui lòng quét mã QR hoặc chuyển khoản chính xác nội dung chuyển khoản để được cộng tiền tự động trong 1-3 phút.";
}
