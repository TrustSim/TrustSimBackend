using MediatR;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

/// <summary>
/// Lệnh yêu cầu gửi mã OTP qua Email để phục vụ ký hợp đồng chuyển nhượng SIM.
/// </summary>
/// <param name="TradeId">ID của giao dịch chuyển nhượng.</param>
/// <param name="UserId">ID của người dùng đang yêu cầu ký.</param>
public record SendContractSigningOtpCommand(
    Guid TradeId,
    Guid UserId
) : IRequest<SendOtpResponseDto>;
