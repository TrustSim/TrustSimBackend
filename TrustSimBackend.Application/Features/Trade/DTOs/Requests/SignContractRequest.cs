namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

/// <summary>
/// DTO chứa mã OTP người dùng gửi lên để thực hiện ký số hợp đồng.
/// </summary>
public record SignContractRequest(string OtpCode);
