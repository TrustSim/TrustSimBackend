using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Dispute.DTOs.Responses;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Dispute.DTOs.Requests;

public record GetDisputesQuery(
    Guid? UserId,
    DisputeQueryFilter Filter
) : IRequest<PagedResult<DisputeTicketDto>>;
