using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Trade.Helpers;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Infrastructure.Persistence;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Quét định kỳ các giao dịch đang chuyển nhượng SIM sau khi ký hợp đồng số 2
/// để gửi thông báo nhắc hạn (bàn giao SIM, nghiệm thu im lặng, và duyệt hồ sơ tồn đọng) có chống trùng lặp.
/// </summary>
public class TradeDeadlineReminderService : ITradeDeadlineReminderService
{
    private readonly TrustSimDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly TradeDeadlineSettings _deadlineSettings;
    private readonly ILogger<TradeDeadlineReminderService> _logger;

    public TradeDeadlineReminderService(
        TrustSimDbContext context,
        INotificationService notificationService,
        IOptions<TradeDeadlineSettings> deadlineSettingsOptions,
        ILogger<TradeDeadlineReminderService> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _deadlineSettings = deadlineSettingsOptions.Value;
        _logger = logger;
    }

    public async Task<int> ProcessDueRemindersAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var disputedTradeIds = await _context.DisputeTickets
            .AsNoTracking()
            .Where(d => d.Status == DisputeStatus.OPEN || d.Status == DisputeStatus.INVESTIGATING)
            .Select(d => d.TradeTransactionId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var activeTrades = await _context.TradeTransactions
            .AsNoTracking()
            .Include(t => t.SimNumber)
            .Include(t => t.SimTransfer)
                .ThenInclude(st => st!.TransferProofs)
            .Include(t => t.Auction)
                .ThenInclude(a => a!.TransferProofs)
            .Where(t => t.TradeTransactionStatus == TradeTransactionStatus.PendingTransfer
                && t.SimTransfer != null
                && t.SimTransfer!.SimTransferStatus != SimTransferStatus.COMPLETED
                && t.SimTransfer!.SimTransferStatus != SimTransferStatus.CANCELLED
                && !disputedTradeIds.Contains(t.Id))
            .ToListAsync(cancellationToken);

        var sentCount = 0;
        var transferThresholds = _deadlineSettings.TransferReminderHoursBefore?.Count > 0
            ? _deadlineSettings.TransferReminderHoursBefore
            : new List<int> { 24, 6 };
        var liquidationThresholds = _deadlineSettings.LiquidationReminderHoursBefore?.Count > 0
            ? _deadlineSettings.LiquidationReminderHoursBefore
            : new List<int> { 12, 2 };
        var adminPendingHours = _deadlineSettings.AdminPendingReviewReminderHours > 0
            ? _deadlineSettings.AdminPendingReviewReminderHours
            : 12;

        foreach (var trade in activeTrades)
        {
            var transferStatus = trade.SimTransfer!.SimTransferStatus;
            var phone = trade.SimNumber?.PhoneNumber;

            if (transferStatus == SimTransferStatus.PENDING_SELLER_ACTION && trade.TransferDeadline.HasValue)
            {
                var matchedHour = FindTightestThreshold(trade.TransferDeadline.Value, utcNow, transferThresholds);
                if (matchedHour.HasValue)
                {
                    var reminderKey = $"TRANSFER_SELLER_DUE_{matchedHour.Value}H";
                    if (!await HasReminderAlreadySentAsync(trade.SellerId, trade.Id, reminderKey, cancellationToken))
                    {
                        var remainingHours = Math.Round((trade.TransferDeadline.Value - utcNow).TotalHours, 1);
                        await _notificationService.SendNotificationAsync(
                            userId: trade.SellerId,
                            type: NotificationType.TRADE_DEADLINE_REMINDER,
                            title: $"⏰ Nhắc hạn sang tên SIM {phone} (còn dưới {matchedHour.Value} giờ)",
                            content: $"Giao dịch {trade.TradeTransactionCode} sắp hết hạn bàn giao SIM lúc {trade.TransferDeadline.Value.AddHours(7):HH:mm dd/MM/yyyy} (UTC+7). Vui lòng hoàn tất sang tên tại nhà mạng và nộp bằng chứng trước hạn để tránh bị hủy đơn và phạt cọc.",
                            actionUrl: $"/trade/{trade.Id}",
                            referenceType: "TradeTransaction",
                            referenceId: trade.Id,
                            metadata: new
                            {
                                reminderKey,
                                tradeId = trade.Id,
                                tradeCode = trade.TradeTransactionCode,
                                auctionId = trade.AuctionId,
                                simNumber = phone,
                                deadline = trade.TransferDeadline,
                                remainingHours,
                                stage = "TRANSFER_DEADLINE_REMINDER"
                            },
                            sendEmail: true,
                            cancellationToken: cancellationToken);
                        sentCount++;
                    }
                }
            }
            else if (transferStatus == SimTransferStatus.PENDING_BUYER_ACTION && trade.LiquidationDeadline.HasValue)
            {
                var matchedHour = FindTightestThreshold(trade.LiquidationDeadline.Value, utcNow, liquidationThresholds);
                if (matchedHour.HasValue)
                {
                    var reminderKey = $"LIQUIDATION_BUYER_DUE_{matchedHour.Value}H";
                    if (!await HasReminderAlreadySentAsync(trade.BuyerId, trade.Id, reminderKey, cancellationToken))
                    {
                        var remainingHours = Math.Round((trade.LiquidationDeadline.Value - utcNow).TotalHours, 1);
                        await _notificationService.SendNotificationAsync(
                            userId: trade.BuyerId,
                            type: NotificationType.TRADE_DEADLINE_REMINDER,
                            title: $"⏰ Nhắc hạn nghiệm thu SIM {phone} (còn dưới {matchedHour.Value} giờ)",
                            content: $"Biên bản sang tên của Người bán đã được duyệt cho giao dịch {trade.TradeTransactionCode}. Vui lòng kiểm tra sóng và nộp ảnh nghiệm thu / xác nhận trước {trade.LiquidationDeadline.Value.AddHours(7):HH:mm dd/MM/yyyy} (UTC+7); nếu quá hạn hệ thống sẽ tự động thanh lý và giải ngân Escrow.",
                            actionUrl: $"/trade/{trade.Id}",
                            referenceType: "TradeTransaction",
                            referenceId: trade.Id,
                            metadata: new
                            {
                                reminderKey,
                                tradeId = trade.Id,
                                tradeCode = trade.TradeTransactionCode,
                                auctionId = trade.AuctionId,
                                simNumber = phone,
                                deadline = trade.LiquidationDeadline,
                                remainingHours,
                                stage = "LIQUIDATION_DEADLINE_REMINDER"
                            },
                            sendEmail: true,
                            cancellationToken: cancellationToken);
                        sentCount++;
                    }
                }
            }
            else if (transferStatus is SimTransferStatus.SELLER_PROOF_SUBMITTED or SimTransferStatus.BUYER_PROOF_SUBMITTED)
            {
                var pendingProof = SimTransferMappingHelper.GetMergedProofs(trade)
                    .Where(p => p.ProofStatus == ProofStatus.PENDING)
                    .OrderBy(p => p.CreatedDate)
                    .FirstOrDefault();

                if (pendingProof != null)
                {
                    var activeDeadline = pendingProof.ProofType == TransferProofType.SELLER_HANDOVER
                        ? trade.TransferDeadline
                        : trade.LiquidationDeadline;

                    var waitedLongEnough = (utcNow - pendingProof.CreatedDate).TotalHours >= adminPendingHours;
                    var deadlineSoon = activeDeadline.HasValue
                        && activeDeadline.Value > utcNow
                        && (activeDeadline.Value - utcNow).TotalHours <= adminPendingHours;

                    if (waitedLongEnough || deadlineSoon)
                    {
                        var reminderKey = $"ADMIN_REVIEW_DUE_{pendingProof.Id:N}";
                        if (!await HasReminderAlreadySentAsync(null, trade.Id, reminderKey, cancellationToken))
                        {
                            var partyLabel = pendingProof.ProofType == TransferProofType.SELLER_HANDOVER ? "Bên Bán" : "Bên Mua";
                            var staffSent = await _notificationService.SendNotificationToRolesAsync(
                                new[] { UserRole.ADMIN, UserRole.CONSULTANT },
                                NotificationType.TRADE_DEADLINE_REMINDER,
                                $"⏰ Hồ sơ bằng chứng {partyLabel} đang chờ duyệt quá hạn nhắc",
                                $"Bộ bằng chứng {partyLabel} của SIM {phone} (Mã GD: {trade.TradeTransactionCode}) đang ở trạng thái PENDING cần kiểm duyệt.",
                                actionUrl: $"/transfer-review?tradeId={trade.Id}",
                                referenceType: "TradeTransaction",
                                referenceId: trade.Id,
                                metadata: new
                                {
                                    reminderKey,
                                    tradeId = trade.Id,
                                    tradeCode = trade.TradeTransactionCode,
                                    auctionId = trade.AuctionId,
                                    simNumber = phone,
                                    proofId = pendingProof.Id,
                                    proofType = pendingProof.ProofType.ToString(),
                                    deadline = activeDeadline,
                                    stage = "ADMIN_REVIEW_DEADLINE_REMINDER"
                                },
                                sendEmail: false,
                                cancellationToken: cancellationToken);
                            sentCount += staffSent;
                        }
                    }
                }
            }
        }

        if (sentCount > 0)
        {
            _logger.LogInformation("[TradeDeadlineReminder] Đã phát {Count} thông báo nhắc hạn định kỳ.", sentCount);
        }

        return sentCount;
    }

    private static int? FindTightestThreshold(DateTime deadlineUtc, DateTime utcNow, IEnumerable<int> thresholdsHours)
    {
        var remainingHours = (deadlineUtc - utcNow).TotalHours;
        if (remainingHours <= 0)
        {
            return null;
        }

        var matched = thresholdsHours
            .Where(h => h > 0 && remainingHours <= h)
            .OrderBy(h => h)
            .FirstOrDefault();

        return matched > 0 ? matched : null;
    }

    private Task<bool> HasReminderAlreadySentAsync(
        Guid? userId,
        Guid tradeId,
        string reminderKey,
        CancellationToken cancellationToken)
    {
        var marker = $"\"reminderKey\":\"{reminderKey}\"";
        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => !n.IsDeleted
                && n.NotificationType == NotificationType.TRADE_DEADLINE_REMINDER
                && n.ReferenceType == "TradeTransaction"
                && n.ReferenceId == tradeId
                && n.MetadataJson != null
                && n.MetadataJson.Contains(marker));

        if (userId.HasValue)
        {
            query = query.Where(n => n.UserId == userId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }
}
