using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Dispute.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Dispute.Commands;

public class ResolveDisputeCommandHandler : IRequestHandler<ResolveDisputeCommand, bool>
{
    private readonly IDisputeTicketRepository _disputeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ResolveDisputeCommandHandler(IDisputeTicketRepository disputeRepository, IUnitOfWork unitOfWork)
    {
        _disputeRepository = disputeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(ResolveDisputeCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _disputeRepository.GetByIdAsync(request.TicketId, cancellationToken);
        if (ticket == null)
        {
            throw new NotFoundException($"Không tìm thấy khiếu nại với ID '{request.TicketId}'.");
        }

        ticket.Status = request.ResolutionStatus;
        ticket.ResolvedByAdminId = request.AdminId;
        ticket.AdminResolutionNotes = request.ResolutionNotes.Trim();

        _disputeRepository.Update(ticket);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
