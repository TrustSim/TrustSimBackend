namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Yêu cầu khởi tạo Payment Intent nạp tiền vào ví.
/// </summary>
public class CreateDepositIntentRequest
{
    /// <summary>Số tiền muốn nạp (VNĐ) (Tối thiểu 10.000 VNĐ).</summary>
    public decimal Amount { get; set; }

    /// <summary>Khóa Idempotency Key chống gửi trùng (tùy chọn, hệ thống tự sinh nếu null).</summary>
    public string? IdempotencyKey { get; set; }
}
