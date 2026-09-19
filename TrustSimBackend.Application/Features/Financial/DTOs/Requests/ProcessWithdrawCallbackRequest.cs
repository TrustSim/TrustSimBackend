namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Yêu cầu giả lập Webhook phản hồi từ Ngân hàng / PSP chi hộ cho giao dịch rút tiền.
/// </summary>
public class ProcessWithdrawCallbackRequest
{
    /// <summary>Mã giao dịch rút tiền cần cập nhật (VD: "TS-WD-9812" hoặc TransactionCode).</summary>
    public string TransactionCode { get; set; } = string.Empty;

    /// <summary>Kết quả chi tiền: True = Thành công (Pha 2A), False = Thất bại (Pha 2B - tự hoàn tiền).</summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>Mã tham chiếu giao dịch từ ngân hàng.</summary>
    public string? ExternalReference { get; set; }

    /// <summary>Lý do thất bại nếu IsSuccess = false (VD: "Tài khoản thụ hưởng không tồn tại").</summary>
    public string? FailureReason { get; set; }
}
