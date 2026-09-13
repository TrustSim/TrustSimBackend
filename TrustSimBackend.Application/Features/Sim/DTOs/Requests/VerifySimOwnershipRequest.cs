namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

/// <summary>
/// DTO gửi mã OTP và số điện thoại để hoàn tất xác thực sở hữu SIM (Pha 2 - Proof of Legal Ownership).
/// </summary>
public class VerifySimOwnershipRequest
{
    /// <summary>Số điện thoại cần xác thực.</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Mã OTP 6 chữ số nhận qua tin nhắn SMS.</summary>
    public string OtpCode { get; set; } = string.Empty;
}
