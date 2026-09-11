using MediatR;
using TrustSimBackend.Application.Features.Dispute.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Dispute.DTOs.Requests;

public record CreateDisputeCommand(
    Guid TradeTransactionId,
    string Reason,
    Guid ComplainantId = default
) : IRequest<DisputeTicketDto>;
