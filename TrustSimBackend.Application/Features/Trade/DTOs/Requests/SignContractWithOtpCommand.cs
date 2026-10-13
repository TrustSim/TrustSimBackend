using MediatR;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

/// <summary>
/// Lệnh ký hợp đồng chuyển nhượng số hóa bằng mã xác thực Email OTP.
/// </summary>
/// <param name="TradeId">ID giao dịch chuyển nhượng.</param>
/// <param name="UserId">ID của người dùng thực hiện ký.</param>
/// <param name="OtpCode">Mã OTP gồm 6 chữ số nhận qua email.</param>
/// <param name="IpAddress">Địa chỉ IP của thiết bị ký.</param>
/// <param name="UserAgent">User-Agent của trình duyệt/thiết bị ký.</param>
public record SignContractWithOtpCommand(
    Guid TradeId,
    Guid UserId,
    string OtpCode,
    string? IpAddress = null,
    string? UserAgent = null
) : IRequest<SignContractResultDto>;
