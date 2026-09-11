using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Dispute.DTOs.Requests;
using TrustSimBackend.Application.Features.Dispute.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Dispute.Queries;

public class GetDisputeByIdQueryHandler : IRequestHandler<GetDisputeByIdQuery, DisputeTicketDto>
{
    private readonly IDisputeTicketRepository _disputeRepository;

    public GetDisputeByIdQueryHandler(IDisputeTicketRepository disputeRepository)
    {
        _disputeRepository = disputeRepository;
    }

    public async Task<DisputeTicketDto> Handle(GetDisputeByIdQuery request, CancellationToken cancellationToken)
    {
        var ticket = await _disputeRepository.GetByIdAsync(request.Id, cancellationToken);
        if (ticket == null)
        {
            throw new NotFoundException($"Không tìm thấy khiếu nại với ID '{request.Id}'.");
        }

        return new DisputeTicketDto
        {
            Id = ticket.Id,
            TradeTransactionId = ticket.TradeTransactionId,
            ComplainantId = ticket.ComplainantId,
            ComplainantName = ticket.Complainant?.UserProfile?.FullName ?? ticket.Complainant?.UserName ?? string.Empty,
            Reason = ticket.Reason,
            Status = ticket.Status,
            ResolvedByAdminId = ticket.ResolvedByAdminId,
            AdminResolutionNotes = ticket.AdminResolutionNotes,
            CreatedDate = ticket.CreatedDate
        };
    }
}
