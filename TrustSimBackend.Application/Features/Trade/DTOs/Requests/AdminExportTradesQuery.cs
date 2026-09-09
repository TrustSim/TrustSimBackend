using MediatR;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

public record AdminExportTradesQuery(
    TradeTransactionStatus? Status = null
) : IRequest<byte[]>;
