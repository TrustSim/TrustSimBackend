using MediatR;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Dispute.DTOs.Requests;

public record ResolveDisputeCommand(
    DisputeStatus ResolutionStatus,
    string ResolutionNotes,
    Guid TicketId = default,
    Guid AdminId = default
) : IRequest<bool>;
