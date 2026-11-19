using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Dispute.DTOs.Requests;
using TrustSimBackend.Application.Features.Dispute.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Dispute;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Dispute.Commands;

public class CreateDisputeCommandHandler : IRequestHandler<CreateDisputeCommand, DisputeTicketDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly ILogger<CreateDisputeCommandHandler>? _logger;

    public CreateDisputeCommandHandler(
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        ILogger<CreateDisputeCommandHandler>? logger = null)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<DisputeTicketDto> Handle(CreateDisputeCommand request, CancellationToken cancellationToken)
    {
        var trade = await _unitOfWork.TradeTransactions.GetByIdAsync(request.TradeTransactionId, cancellationToken);
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

        await _unitOfWork.DisputeTickets.AddAsync(ticket, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var isBuyer = trade.BuyerId == request.ComplainantId;
        var isSeller = trade.SellerId == request.ComplainantId;
        Guid? counterpartyId = isBuyer ? trade.SellerId : (isSeller ? trade.BuyerId : null);
        var complainantRole = isBuyer ? "Người mua" : (isSeller ? "Người bán" : "Thành viên");
        var phone = trade.SimNumber?.PhoneNumber;

        var metadata = new
        {
            ticketId = ticket.Id,
            tradeId = trade.Id,
            tradeCode = trade.TradeTransactionCode,
            auctionId = trade.AuctionId,
            simNumber = phone,
            complainantId = request.ComplainantId,
            reason = ticket.Reason,
            stage = "DISPUTE_OPENED"
        };

        try
        {
            if (counterpartyId.HasValue)
            {
                await _notificationService.SendNotificationAsync(
                    counterpartyId.Value,
                    NotificationType.DISPUTE_OPENED,
                    "Giao dịch phát sinh khiếu nại tranh chấp",
                    $"{complainantRole} đã mở khiếu nại cho giao dịch SIM {phone} (Mã GD: {trade.TradeTransactionCode}). Lý do: {ticket.Reason}. Ban Quản Trị sẽ xem xét và liên hệ xử lý.",
                    actionUrl: $"/disputes/{ticket.Id}",
                    referenceType: "DisputeTicket",
                    referenceId: ticket.Id,
                    metadata: metadata,
                    sendEmail: true,
                    cancellationToken: cancellationToken);
            }

            await _notificationService.SendNotificationToRolesAsync(
                new[] { UserRole.ADMIN, UserRole.CONSULTANT },
                NotificationType.DISPUTE_OPENED,
                "Khiếu nại tranh chấp mới cần xử lý",
                $"{complainantRole} đã mở phiếu tranh chấp cho SIM {phone} (Mã GD: {trade.TradeTransactionCode}). Lý do: {ticket.Reason}.",
                actionUrl: $"/disputes/{ticket.Id}",
                referenceType: "DisputeTicket",
                referenceId: ticket.Id,
                metadata: metadata,
                sendEmail: false,
                excludeUserId: request.ComplainantId,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "[CreateDispute] Lỗi khi gửi thông báo mở tranh chấp cho Ticket {TicketId}", ticket.Id);
        }

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
