using MediatR;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

/// <summary>
/// Command dành cho Bên Mua xác nhận đã nhận SIM có sóng, hoàn tất chuyển quyền sở hữu và giải ngân Escrow.
/// </summary>
public record ConfirmSimTransferCommand(
    Guid TradeId,
    Guid UserId
) : IRequest<SimTransferDetailsDto>;
