using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Application.Features.System.DTOs.Responses;
using TrustSimBackend.Domain.Entities.System;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Triển khai Dịch vụ Điều phối Thông báo Đa kênh (Multi-Channel Notification Dispatcher).
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly IAuctionRealtimeNotifier _realtimeNotifier;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        IAuctionRealtimeNotifier realtimeNotifier,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<NotificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _realtimeNotifier = realtimeNotifier;
        _serviceScopeFactory = serviceScopeFactory;
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
        var metadataJson = metadata != null ? JsonSerializer.Serialize(metadata) : null;

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Content = content,
            NotificationType = type,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            MetadataJson = metadataJson,
            ActionUrl = actionUrl,
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
                    var targetEmail = user.Email;
                    var emailSubject = $"[TrustSim] {title}";
                    var emailHtml = $@"
                        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 8px;'>
                            <h2 style='color: #2563eb;'>{title}</h2>
                            <p style='color: #334155; font-size: 15px; line-height: 1.6;'>{content}</p>
                            {(string.IsNullOrWhiteSpace(actionUrl) ? "" : $"<div style='margin-top: 24px;'><a href='{actionUrl}' style='background-color: #2563eb; color: #ffffff; padding: 10px 20px; text-decoration: none; border-radius: 6px; font-weight: bold;'>Xem Chi Tiết</a></div>")}
                            <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;' />
                            <p style='font-size: 12px; color: #94a3b8;'>Đây là email tự động từ Sàn Giao Dịch TrustSim. Vui lòng không phản hồi email này.</p>
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
