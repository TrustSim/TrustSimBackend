using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Dispute.DTOs.Requests;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Dispute.Commands;

public class ResolveDisputeCommandHandler : IRequestHandler<ResolveDisputeCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ResolveDisputeCommandHandler>? _logger;

    public ResolveDisputeCommandHandler(
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        ILogger<ResolveDisputeCommandHandler>? logger = null)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<bool> Handle(ResolveDisputeCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _unitOfWork.DisputeTickets.GetByIdAsync(request.TicketId, cancellationToken);
        if (ticket == null)
        {
            throw new NotFoundException($"Không tìm thấy khiếu nại với ID '{request.TicketId}'.");
        }

        ticket.Status = request.ResolutionStatus;
        ticket.ResolvedByAdminId = request.AdminId;
        ticket.AdminResolutionNotes = request.ResolutionNotes.Trim();

        _unitOfWork.DisputeTickets.Update(ticket);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var trade = await _unitOfWork.TradeTransactions.GetByIdAsync(ticket.TradeTransactionId, cancellationToken)
            ?? ticket.TradeTransaction;

        if (trade != null)
        {
            var resolutionLabel = ticket.Status switch
            {
                DisputeStatus.RESOLVED_FAVOR_SELLER => "Phán quyết có lợi cho Người bán",
                DisputeStatus.RESOLVED_FAVOR_BUYER => "Phán quyết có lợi cho Người mua",
                DisputeStatus.INVESTIGATING => "Đang trong quá trình điều tra",
                _ => ticket.Status.ToString()
            };

            var phone = trade.SimNumber?.PhoneNumber;
            var metadata = new
            {
                ticketId = ticket.Id,
                tradeId = trade.Id,
                tradeCode = trade.TradeTransactionCode,
                auctionId = trade.AuctionId,
                simNumber = phone,
                resolutionStatus = ticket.Status.ToString(),
                resolutionNotes = ticket.AdminResolutionNotes,
                stage = "DISPUTE_RESOLVED"
            };

            try
            {
                var recipientIds = new[] { trade.BuyerId, trade.SellerId }.Distinct();
                foreach (var recipientId in recipientIds)
                {
                    await _notificationService.SendNotificationAsync(
                        recipientId,
                        NotificationType.DISPUTE_RESOLVED,
                        $"Kết quả xử lý tranh chấp ({resolutionLabel})",
                        $"Ban Quản Trị đã ban hành phán quyết cho khiếu nại giao dịch SIM {phone} (Mã GD: {trade.TradeTransactionCode}): {resolutionLabel}. Ghi chú: {ticket.AdminResolutionNotes}",
                        actionUrl: $"/disputes/{ticket.Id}",
                        referenceType: "DisputeTicket",
                        referenceId: ticket.Id,
                        metadata: metadata,
                        sendEmail: true,
                        cancellationToken: cancellationToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogWarning(ex, "[ResolveDispute] Lỗi khi gửi thông báo kết quả tranh chấp Ticket {TicketId}", ticket.Id);
            }
        }

        return true;
    }
}
