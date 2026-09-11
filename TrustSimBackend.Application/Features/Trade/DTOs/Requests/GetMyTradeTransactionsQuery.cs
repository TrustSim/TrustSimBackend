using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

public record GetMyTradeTransactionsQuery(
    Guid UserId = default,
    TradeTransactionStatus? Status = null,
    int PageIndex = 1,
    int PageSize = 20
) : IRequest<PagedResult<TradeTransactionDto>>;
