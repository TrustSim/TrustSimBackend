namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// DTO gửi lên từ Client khi yêu cầu nạp tiền vào ví.
/// Không yêu cầu truyền UserId vì hệ thống tự trích xuất từ JWT Claims.
/// </summary>
public class CreateDepositRequest
{
    /// <summary>
    /// Số tiền cần nạp (VNĐ) - Tối thiểu 10.000 VNĐ.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Ghi chú nạp tiền (tùy chọn).
    /// </summary>
    public string? Note { get; set; }
}
