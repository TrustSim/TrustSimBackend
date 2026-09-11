using MediatR;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

public record CreateTradeTransactionCommand(
    Guid AuctionId,
    Guid SellerId,
    Guid SimNumberId,
    decimal EscrowAmount,
    Guid BuyerId = default
) : IRequest<TradeTransactionDto>;
