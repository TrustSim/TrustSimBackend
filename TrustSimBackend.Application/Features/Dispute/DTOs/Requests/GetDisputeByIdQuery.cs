using MediatR;
using TrustSimBackend.Application.Features.Dispute.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Dispute.DTOs.Requests;

public record GetDisputeByIdQuery(Guid Id) : IRequest<DisputeTicketDto>;
