namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// DTO gửi lên từ Admin khi từ chối yêu cầu rút tiền.
/// </summary>
public class RejectWithdrawRequest
{
    /// <summary>
    /// Lý do từ chối yêu cầu rút tiền.
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}
