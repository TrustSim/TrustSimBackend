using MediatR;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

/// <summary>
/// Query lấy thông tin chi tiết tiến trình bàn giao SIM và giải ngân Escrow của giao dịch.
/// </summary>
public record GetSimTransferDetailsQuery(
    Guid TradeId,
    Guid UserId,
    bool IsAdmin = false
) : IRequest<SimTransferDetailsDto>;
