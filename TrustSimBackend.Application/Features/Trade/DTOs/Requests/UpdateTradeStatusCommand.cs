using MediatR;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

public record UpdateTradeStatusCommand(
    Guid TradeId = default,
    TradeTransactionStatus NewStatus = default
) : IRequest<bool>;
