using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Dispute.DTOs;
using TrustSimBackend.Domain.Entities.Dispute;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Dispute.Commands;

public record CreateDisputeCommand(
    Guid ComplainantId,
    Guid TradeTransactionId,
    string Reason
) : IRequest<ApiResponse<DisputeTicketDto>>;

public class CreateDisputeCommandHandler : IRequestHandler<CreateDisputeCommand, ApiResponse<DisputeTicketDto>>
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

    public async Task<ApiResponse<DisputeTicketDto>> Handle(CreateDisputeCommand request, CancellationToken cancellationToken)
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

        var dto = new DisputeTicketDto
        {
            Id = ticket.Id,
            TradeTransactionId = ticket.TradeTransactionId,
            ComplainantId = ticket.ComplainantId,
            Reason = ticket.Reason,
            Status = ticket.Status,
            CreatedDate = ticket.CreatedDate
        };

        return ApiResponse<DisputeTicketDto>.SuccessResult(dto, "Mở khiếu nại tranh chấp thành công.");
    }
}

public record ResolveDisputeCommand(
    Guid TicketId,
    Guid AdminId,
    DisputeStatus ResolutionStatus,
    string ResolutionNotes
) : IRequest<ApiResponse<bool>>;

public class ResolveDisputeCommandHandler : IRequestHandler<ResolveDisputeCommand, ApiResponse<bool>>
{
    private readonly IDisputeTicketRepository _disputeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ResolveDisputeCommandHandler(IDisputeTicketRepository disputeRepository, IUnitOfWork unitOfWork)
    {
        _disputeRepository = disputeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<bool>> Handle(ResolveDisputeCommand request, CancellationToken cancellationToken)
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

        return ApiResponse<bool>.SuccessResult(true, "Xử lý phán quyết khiếu nại thành công.");
    }
}
