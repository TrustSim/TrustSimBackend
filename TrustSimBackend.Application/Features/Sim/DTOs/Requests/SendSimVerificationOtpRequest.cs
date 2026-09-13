namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

/// <summary>
/// DTO yêu cầu gửi mã OTP để xác nhận quyền giữ SIM vật lý (Pha 1 - Proof of Possession).
/// </summary>
public class SendSimVerificationOtpRequest
{
    /// <summary>Số điện thoại cần xác thực quyền sở hữu (VD: 0909123456 hoặc 84909123456).</summary>
    public string PhoneNumber { get; set; } = string.Empty;
}
