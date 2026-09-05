using MediatR;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

public record GetTradeTransactionByIdQuery(
    Guid Id = default,
    Guid UserId = default
) : IRequest<TradeTransactionDto>;
