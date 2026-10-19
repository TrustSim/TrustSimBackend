using MediatR;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

public record GetTradeTransactionByCodeQuery(
    string TradeCode,
    Guid UserId,
    bool IsStaff = false
) : IRequest<TradeTransactionDto>;
