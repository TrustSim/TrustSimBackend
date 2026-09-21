namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// DTO gửi lên từ Admin khi điều chỉnh số dư ví thủ công.
/// </summary>
public class AdminAdjustBalanceRequest
{
    /// <summary>
    /// Số tiền điều chỉnh (Dương: Cộng tiền, Âm: Trừ tiền).
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Lý do điều chỉnh số dư ví.
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}
