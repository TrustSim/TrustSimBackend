namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// DTO gửi lên từ Admin khi phê duyệt yêu cầu rút tiền.
/// </summary>
public class ApproveWithdrawRequest
{
    /// <summary>
    /// Ghi chú phê duyệt của Admin (tùy chọn).
    /// </summary>
    public string? Note { get; set; }
}
