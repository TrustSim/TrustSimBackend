namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// DTO yêu cầu xác nhận chi trả rút tiền thất bại và hoàn tiền (Pha 2B Sandbox).
/// </summary>
public class ProcessWithdrawFailedRequest
{
    public string TransactionCode { get; set; } = string.Empty;
    public string FailureReason { get; set; } = "Ngân hàng từ chối chi hộ";
}
