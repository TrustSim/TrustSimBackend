using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Dispute.DTOs.Requests;
using TrustSimBackend.Application.Features.Dispute.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Dispute;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Dispute.Commands;

public class CreateDisputeCommandHandler : IRequestHandler<CreateDisputeCommand, DisputeTicketDto>
{
    private readonly IDisputeTicketRepository _disputeRepository;
    private readonly ITradeTransactionRepository _tradeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateDisputeCommandHandler(
        IDisputeTicketRepository disputeRepository,
        ITradeTransactionRepository tradeRepository,
        IUnitOfWork unitOfWork)
    {
        _disputeRepository = disputeRepository;
        _tradeRepository = tradeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<DisputeTicketDto> Handle(CreateDisputeCommand request, CancellationToken cancellationToken)
    {
        var trade = await _tradeRepository.GetByIdAsync(request.TradeTransactionId, cancellationToken);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch với ID '{request.TradeTransactionId}'.");
        }

        var ticket = new DisputeTicket
        {
            Id = Guid.NewGuid(),
            TradeTransactionId = request.TradeTransactionId,
            ComplainantId = request.ComplainantId,
            Reason = request.Reason.Trim(),
            Status = DisputeStatus.OPEN
        };

        await _disputeRepository.AddAsync(ticket, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new DisputeTicketDto
        {
            Id = ticket.Id,
            TradeTransactionId = ticket.TradeTransactionId,
            ComplainantId = ticket.ComplainantId,
            Reason = ticket.Reason,
            Status = ticket.Status,
            CreatedDate = ticket.CreatedDate
        };
    }
}
