namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// DTO gửi lên từ Client khi yêu cầu rút tiền từ ví về tài khoản ngân hàng.
/// Không yêu cầu truyền UserId vì hệ thống tự trích xuất từ JWT Claims.
/// </summary>
public class CreateWithdrawRequest
{
    /// <summary>
    /// Số tiền rút (VNĐ) - Tối thiểu 50.000 VNĐ.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Mã ngân hàng nhận (VD: VCB, TCB, MB, ACB, ICB).
    /// </summary>
    public string DestinationBankCode { get; set; } = string.Empty;

    /// <summary>
    /// Tên ngân hàng nhận.
    /// </summary>
    public string DestinationBankName { get; set; } = string.Empty;

    /// <summary>
    /// Số tài khoản ngân hàng nhận tiền.
    /// </summary>
    public string DestinationAccountNumber { get; set; } = string.Empty;

    /// <summary>
    /// Tên chủ tài khoản nhận tiền (Viết hoa không dấu).
    /// </summary>
    public string DestinationAccountHolderName { get; set; } = string.Empty;

    /// <summary>
    /// Ghi chú rút tiền (tùy chọn).
    /// </summary>
    public string? Note { get; set; }
}
