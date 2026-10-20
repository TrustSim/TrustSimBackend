using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

public record GetMyTradeTransactionsQuery(
    Guid UserId,
    MyTradeQueryFilter Filter
) : IRequest<PagedResult<TradeTransactionDto>>;
