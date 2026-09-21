namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// DTO yêu cầu xác nhận chi trả rút tiền thành công (Pha 2A Sandbox).
/// </summary>
public class ProcessWithdrawSuccessRequest
{
    public string TransactionCode { get; set; } = string.Empty;
    public string? ExternalBankRef { get; set; }
}
