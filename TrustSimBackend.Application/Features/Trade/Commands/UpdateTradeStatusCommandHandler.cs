using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Trade.Commands;

public class UpdateTradeStatusCommandHandler : IRequestHandler<UpdateTradeStatusCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly ILogger<UpdateTradeStatusCommandHandler>? _logger;

    public UpdateTradeStatusCommandHandler(
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        ILogger<UpdateTradeStatusCommandHandler>? logger = null)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<bool> Handle(UpdateTradeStatusCommand request, CancellationToken cancellationToken)
    {
        var trade = await _unitOfWork.TradeTransactions.GetByIdAsync(request.TradeId, cancellationToken);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch với ID '{request.TradeId}'.");
        }

        var oldStatus = trade.TradeTransactionStatus;
        if (oldStatus == request.NewStatus)
        {
            return true;
        }

        trade.TradeTransactionStatus = request.NewStatus;
        _unitOfWork.TradeTransactions.Update(trade);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var isAdminCancel = request.NewStatus == TradeTransactionStatus.Cancelled_ByAdmin;
        var notifType = isAdminCancel
            ? NotificationType.TRADE_CANCELLED_BY_ADMIN
            : NotificationType.TRADE_STATUS_UPDATED;
        var phone = trade.SimNumber?.PhoneNumber;
        var title = isAdminCancel
            ? "Giao dịch chuyển nhượng SIM đã bị Quản trị viên hủy"
            : "Trạng thái giao dịch chuyển nhượng SIM đã được cập nhật";
        var content = isAdminCancel
            ? $"Quản trị viên đã hủy giao dịch SIM {phone} (Mã GD: {trade.TradeTransactionCode}). Vui lòng kiểm tra chi tiết giao dịch hoặc liên hệ bộ phận hỗ trợ sàn."
            : $"Quản trị viên đã cập nhật trạng thái giao dịch SIM {phone} (Mã GD: {trade.TradeTransactionCode}) từ {oldStatus} sang {request.NewStatus}.";

        var metadata = new
        {
            tradeId = trade.Id,
            tradeCode = trade.TradeTransactionCode,
            auctionId = trade.AuctionId,
            simNumber = phone,
            oldStatus = oldStatus.ToString(),
            newStatus = request.NewStatus.ToString(),
            stage = "TRADE_STATUS_UPDATED"
        };

        try
        {
            foreach (var recipientId in new[] { trade.BuyerId, trade.SellerId }.Distinct())
            {
                await _notificationService.SendNotificationAsync(
                    recipientId,
                    notifType,
                    title,
                    content,
                    actionUrl: $"/trade/{trade.Id}",
                    referenceType: "TradeTransaction",
                    referenceId: trade.Id,
                    metadata: metadata,
                    sendEmail: true,
                    cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "[UpdateTradeStatus] Lỗi khi gửi thông báo cập nhật trạng thái Trade {TradeId}", trade.Id);
        }

        return true;
    }
}
