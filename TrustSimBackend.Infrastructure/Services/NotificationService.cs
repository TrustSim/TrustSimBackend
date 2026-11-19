using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Application.Features.System.DTOs.Responses;
using TrustSimBackend.Domain.Entities.System;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Triển khai Dịch vụ Điều phối Thông báo Đa kênh (Multi-Channel Notification Dispatcher).
/// </summary>
public class NotificationService : INotificationService
{
    private const int MaxTitleLength = 200;
    private const int MaxContentLength = 1000;
    private const int MaxActionUrlLength = 500;
    private const int MaxReferenceTypeLength = 50;
    private const int MaxMetadataJsonLength = 4000;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly IAuctionRealtimeNotifier _realtimeNotifier;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        IAuctionRealtimeNotifier realtimeNotifier,
        IServiceScopeFactory serviceScopeFactory,
        IConfiguration configuration,
        ILogger<NotificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _realtimeNotifier = realtimeNotifier;
        _serviceScopeFactory = serviceScopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Notification> SendNotificationAsync(
        Guid userId,
        NotificationType type,
        string title,
        string content,
        string? actionUrl = null,
        string? referenceType = null,
        Guid? referenceId = null,
        object? metadata = null,
        bool sendEmail = false,
        CancellationToken cancellationToken = default)
    {
        var resolvedUrl = actionUrl;

        // Tự động kiểm tra quyền và chỉ tự gắn link PDF CDN cho các thông báo phát hành văn bản (VB1/VB2/VB3),
        // giữ nguyên ActionUrl thao tác UI cho các thông báo nghiệp vụ (từ chối/nộp bằng chứng, nhắc hạn, hủy...).
        if (referenceType == "TradeTransaction" && referenceId.HasValue)
        {
            resolvedUrl = await ResolveTradeActionUrlAsync(userId, type, referenceId.Value, resolvedUrl, cancellationToken);
        }

        var safeTitle = TruncateWithEllipsis(title, MaxTitleLength) ?? string.Empty;
        var safeContent = TruncateWithEllipsis(content, MaxContentLength) ?? string.Empty;
        var safeActionUrl = TruncateWithEllipsis(resolvedUrl, MaxActionUrlLength);
        var safeReferenceType = TruncateWithEllipsis(referenceType, MaxReferenceTypeLength);
        var metadataJson = SerializeMetadataWithinLimit(metadata, MaxMetadataJsonLength);

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = safeTitle,
            Content = safeContent,
            NotificationType = type,
            ReferenceType = safeReferenceType,
            ReferenceId = referenceId,
            MetadataJson = metadataJson,
            ActionUrl = safeActionUrl,
            IsRead = false,
            CreatedDate = DateTime.UtcNow
        };

        // 1. Lưu CSDL In-App
        await _unitOfWork.Notifications.AddAsync(notification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 2. Push WebSocket Realtime tới phòng cá nhân user_{userId}
        try
        {
            var dto = new NotificationDto
            {
                Id = notification.Id,
                UserId = notification.UserId,
                Title = notification.Title,
                Content = notification.Content,
                NotificationType = notification.NotificationType,
                ReferenceType = notification.ReferenceType,
                ReferenceId = notification.ReferenceId,
                MetadataJson = notification.MetadataJson,
                ActionUrl = notification.ActionUrl,
                IsRead = notification.IsRead,
                ReadAt = notification.ReadAt,
                CreatedDate = notification.CreatedDate
            };

            await _realtimeNotifier.NotifyPersonalNotificationAsync(userId, dto, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NotificationService] Lỗi khi push SignalR realtime tới user {UserId}", userId);
        }

        // 3. Gửi Email tự động nếu được yêu cầu (chạy ngầm trong Background Task để không block API)
        if (sendEmail)
        {
            try
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
                if (user != null && !string.IsNullOrWhiteSpace(user.Email))
                {
                    var emailActionUrl = resolvedUrl;

                    if (!string.IsNullOrWhiteSpace(emailActionUrl) && emailActionUrl.StartsWith("/"))
                    {
                        var frontendUrl = _configuration["FrontendUrl"];
                        if (string.IsNullOrWhiteSpace(frontendUrl) || !frontendUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            frontendUrl = "http://localhost:5500";
                        }
                        emailActionUrl = $"{frontendUrl.TrimEnd('/')}{emailActionUrl}";
                    }

                    var isPdfLink = !string.IsNullOrWhiteSpace(emailActionUrl) && 
                        (emailActionUrl.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) || 
                         emailActionUrl.Contains("/contracts/") || 
                         emailActionUrl.Contains(".supabase.co"));
                    var buttonText = isPdfLink ? "📄 Tải Tệp Tin PDF Chính Thức (Supabase)" : "Xem Chi Tiết";
                    var buttonBg = isPdfLink ? "#10b981" : "#2563eb";

                    var targetEmail = user.Email;
                    var emailSubject = $"[TrustSim] {title}";
                    var emailHtml = $@"
                        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 10px; background-color: #ffffff;'>
                            <div style='text-align: center; margin-bottom: 20px;'>
                                <h3 style='margin: 0; color: #1e40af;'>SÀN ĐẤU GIÁ SIM TRUSTSIM</h3>
                                <p style='margin: 4px 0 0; color: #64748b; font-size: 13px;'>Hệ thống Giao dịch & Quản lý Hợp đồng Điện tử Bảo đảm</p>
                            </div>
                            <div style='background: #f8fafc; border-radius: 8px; padding: 18px; border-left: 4px solid #2563eb; margin-bottom: 20px;'>
                                <h2 style='color: #0f172a; margin: 0 0 10px; font-size: 18px;'>{title}</h2>
                                <p style='color: #334155; font-size: 14.5px; line-height: 1.6; margin: 0;'>{content}</p>
                            </div>
                            {(string.IsNullOrWhiteSpace(emailActionUrl) ? "" : $@"
                            <div style='text-align: center; margin: 28px 0;'>
                                <a href='{emailActionUrl}' target='_blank' style='background-color: {buttonBg}; color: #ffffff; padding: 14px 28px; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 15px; display: inline-block; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);'>{buttonText}</a>
                            </div>
                            <p style='font-size: 12.5px; color: #64748b; text-align: center; margin: 0;'>Đường dẫn tải trực tiếp: <br/><a href='{emailActionUrl}' target='_blank' style='color: #2563eb; word-break: break-all;'>{emailActionUrl}</a></p>")}
                            <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;' />
                            <p style='font-size: 12px; color: #94a3b8; text-align: center; margin: 0;'>Đây là email tự động từ Sàn Giao Dịch TrustSim. Vui lòng không phản hồi email này.</p>
                        </div>";

                    // Gửi email bất đồng bộ ngầm, không block luồng xử lý chính của Command
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var scope = _serviceScopeFactory.CreateScope();
                            var scopedEmailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                            await scopedEmailService.SendEmailAsync(targetEmail, emailSubject, emailHtml, cts.Token);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "[NotificationService] Lỗi khi gửi email ngầm tới user {UserId} ({Email})", userId, targetEmail);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[NotificationService] Lỗi khi chuẩn bị gửi email tới user {UserId}", userId);
            }
        }

        return notification;
    }

    public async Task<int> SendNotificationToRolesAsync(
        IReadOnlyCollection<UserRole> roles,
        NotificationType type,
        string title,
        string content,
        string? actionUrl = null,
        string? referenceType = null,
        Guid? referenceId = null,
        object? metadata = null,
        bool sendEmail = false,
        Guid? excludeUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (roles == null || roles.Count == 0)
        {
            return 0;
        }

        var recipientIds = new HashSet<Guid>();
        foreach (var role in roles.Distinct())
        {
            var filter = new AdminUsersQueryFilter
            {
                Role = role,
                IsActive = true,
                PageIndex = 1,
                PageSize = 500
            };

            var (users, _) = await _unitOfWork.Users.GetPagedAsync(filter, cancellationToken);
            foreach (var user in users)
            {
                if (excludeUserId.HasValue && user.Id == excludeUserId.Value)
                {
                    continue;
                }

                recipientIds.Add(user.Id);
            }
        }

        var sentCount = 0;
        foreach (var recipientId in recipientIds)
        {
            try
            {
                await SendNotificationAsync(
                    recipientId,
                    type,
                    title,
                    content,
                    actionUrl,
                    referenceType,
                    referenceId,
                    metadata,
                    sendEmail,
                    cancellationToken);
                sentCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[NotificationService] Lỗi khi gửi thông báo theo vai trò tới user {UserId}", recipientId);
            }
        }

        return sentCount;
    }

    private async Task<string?> ResolveTradeActionUrlAsync(
        Guid userId,
        NotificationType type,
        Guid tradeId,
        string? requestedActionUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var tradeUow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var trade = await tradeUow.TradeTransactions.GetByIdAsync(tradeId, cancellationToken);
            if (trade == null)
            {
                return requestedActionUrl;
            }

            var isParticipant = trade.BuyerId == userId || trade.SellerId == userId;
            if (!isParticipant)
            {
                var recipient = await tradeUow.Users.GetByIdAsync(userId, cancellationToken);
                var isStaff = recipient?.Role is UserRole.ADMIN or UserRole.CONSULTANT;
                return isStaff ? requestedActionUrl : null;
            }

            if (!IsDocumentCentricNotificationType(type) && !string.IsNullOrWhiteSpace(requestedActionUrl))
            {
                return requestedActionUrl;
            }

            var isAlreadyPdfOrSupabase = !string.IsNullOrWhiteSpace(requestedActionUrl) &&
                (requestedActionUrl.Contains(".supabase.co", StringComparison.OrdinalIgnoreCase) ||
                 requestedActionUrl.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));

            if (isAlreadyPdfOrSupabase)
            {
                return requestedActionUrl;
            }

            var contract = trade.TransferContract;
            if (type is NotificationType.TRADE_COMPLETED_LIQUIDATED or NotificationType.ESCROW_RELEASED_SELLER_PAYOUT)
            {
                return !string.IsNullOrWhiteSpace(contract?.LiquidationDocumentUrl)
                    ? contract.LiquidationDocumentUrl
                    : requestedActionUrl;
            }

            if (type == NotificationType.CONTRACT_FULLY_SIGNED)
            {
                return !string.IsNullOrWhiteSpace(contract?.DocumentUrl)
                    ? contract.DocumentUrl
                    : requestedActionUrl;
            }

            if (!string.IsNullOrWhiteSpace(contract?.LiquidationDocumentUrl))
            {
                return contract.LiquidationDocumentUrl;
            }

            if (!string.IsNullOrWhiteSpace(contract?.DocumentUrl))
            {
                return contract.DocumentUrl;
            }

            if (!string.IsNullOrWhiteSpace(contract?.WinningNoticeUrl))
            {
                return contract.WinningNoticeUrl;
            }

            var pdfService = scope.ServiceProvider.GetService<IPdfContractService>();
            if (pdfService != null)
            {
                var noticeUrl = await pdfService.EnsureWinningNoticeUploadedAsync(trade.Id, cancellationToken);
                if (!string.IsNullOrWhiteSpace(noticeUrl))
                {
                    _logger.LogInformation("[NotificationService] ✅ Đã tự động tạo và gán link Supabase PDF ({Url}) cho thông báo {TradeCode}", noticeUrl, trade.TradeTransactionCode);
                    return noticeUrl;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NotificationService] Lỗi khi kiểm tra/tạo link cho Trade {TradeId}", tradeId);
        }

        return requestedActionUrl;
    }

    private static bool IsDocumentCentricNotificationType(NotificationType type) => type is
        NotificationType.AUCTION_WON or
        NotificationType.CONTRACT_SIGNING_REQUIRED or
        NotificationType.CONTRACT_FULLY_SIGNED or
        NotificationType.TRADE_COMPLETED_LIQUIDATED or
        NotificationType.ESCROW_RELEASED_SELLER_PAYOUT;

    private static string? TruncateWithEllipsis(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return maxLength <= 3
            ? value[..maxLength]
            : string.Concat(value.AsSpan(0, maxLength - 3), "...");
    }

    private static string? SerializeMetadataWithinLimit(object? metadata, int maxLength)
    {
        if (metadata == null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(metadata);
        if (json.Length <= maxLength)
        {
            return json;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                var compacted = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    compacted[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                        ? TruncateWithEllipsis(prop.Value.GetString(), 400)
                        : prop.Value.Clone();
                }

                var compactedJson = JsonSerializer.Serialize(compacted);
                if (compactedJson.Length <= maxLength)
                {
                    return compactedJson;
                }

                var essentialKeys = new HashSet<string>(StringComparer.Ordinal)
                {
                    "tradeId", "tradeCode", "auctionId", "ticketId", "simNumber",
                    "proofId", "proofType", "reminderKey", "stage"
                };
                var minimal = compacted
                    .Where(kv => essentialKeys.Contains(kv.Key))
                    .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
                minimal["truncated"] = true;

                var minimalJson = JsonSerializer.Serialize(minimal);
                if (minimalJson.Length <= maxLength)
                {
                    return minimalJson;
                }
            }
        }
        catch (JsonException)
        {
            // Fall back to safe minimal JSON marker below
        }

        return "{\"truncated\":true}";
    }

    public async Task SendAuctionRoomNotificationAsync(
        Guid auctionId,
        string message,
        string type = "INFO",
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Chỉ phát sóng tới đúng phòng phiên đấu giá auction_{auctionId}, KHÔNG phát toàn hệ thống
            await _realtimeNotifier.NotifyAuctionNotificationAsync(auctionId, message, type, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NotificationService] Lỗi khi gửi thông báo tới phòng đấu giá {AuctionId}", auctionId);
        }
    }

    public async Task BroadcastNotificationAsync(
        string title,
        string content,
        string? actionUrl = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Phát thông báo toàn sàn (chỉ dùng cho Admin Broadcast)
            await _realtimeNotifier.NotifyAuctionNotificationAsync(Guid.Empty, content, "SYSTEM_BROADCAST", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NotificationService] Lỗi khi broadcast notification toàn hệ thống.");
        }
    }
}
