using MediatR;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

public record AdminExportTradesQuery(
    AdminTradeQueryFilter Filter
) : IRequest<byte[]>;
