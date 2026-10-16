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

        // Tự động kiểm tra và ưu tiên trỏ thẳng tới file PDF lưu trên Supabase Storage CDN cho các thông báo giao dịch/hợp đồng
        // QUY TẮC: CHỈ Người mua (Người thắng cuộc) và Người bán mới được quyền nhận link tài liệu PDF
        if (referenceType == "TradeTransaction" && referenceId.HasValue)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var tradeUow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var trade = await tradeUow.TradeTransactions.GetByIdAsync(referenceId.Value, cancellationToken);

                if (trade != null)
                {
                    // Kiểm tra người nhận có phải là Người mua hoặc Người bán hay không
                    var isAuthorizedParticipant = trade.BuyerId == userId || trade.SellerId == userId;
                    if (!isAuthorizedParticipant)
                    {
                        // Tuyệt đối không cung cấp link tài liệu giao dịch/PDF cho người ngoài
                        resolvedUrl = null;
                    }
                    else
                    {
                        var isAlreadySupabase = !string.IsNullOrWhiteSpace(resolvedUrl) && 
                            (resolvedUrl.Contains(".supabase.co", StringComparison.OrdinalIgnoreCase) || 
                             resolvedUrl.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));

                        if (!isAlreadySupabase)
                        {
                            // 1. Ưu tiên cao nhất: Biên bản nghiệm thu và thanh lý đã niêm phong
                            if (!string.IsNullOrWhiteSpace(trade.TransferContract?.LiquidationDocumentUrl))
                            {
                                resolvedUrl = trade.TransferContract.LiquidationDocumentUrl;
                            }
                            // 2. Ưu tiên 2: Hợp đồng chuyển nhượng 3 bên đã niêm phong PAdES X.509
                            else if (!string.IsNullOrWhiteSpace(trade.TransferContract?.DocumentUrl))
                            {
                                resolvedUrl = trade.TransferContract.DocumentUrl;
                            }
                            // 3. Ưu tiên 3: Phiếu chốt giá trúng đấu giá đã lưu trên CDN
                            else if (!string.IsNullOrWhiteSpace(trade.TransferContract?.WinningNoticeUrl))
                            {
                                resolvedUrl = trade.TransferContract.WinningNoticeUrl;
                            }
                            // 4. Nếu chưa có Phiếu chốt giá trúng -> Gọi IPdfContractService tự động sinh PDF và upload Supabase CDN ngay lập tức
                            else
                            {
                                var pdfService = scope.ServiceProvider.GetService<IPdfContractService>();
                                if (pdfService != null)
                                {
                                    var noticeUrl = await pdfService.EnsureWinningNoticeUploadedAsync(trade.Id, cancellationToken);
                                    if (!string.IsNullOrWhiteSpace(noticeUrl))
                                    {
                                        resolvedUrl = noticeUrl;
                                        _logger.LogInformation("[NotificationService] ✅ Đã tự động tạo và gán link Supabase PDF ({Url}) cho thông báo {TradeCode}", noticeUrl, trade.TradeTransactionCode);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[NotificationService] Lỗi khi kiểm tra/tạo file PDF Supabase cho Trade {TradeId}", referenceId.Value);
            }
        }

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
            ActionUrl = resolvedUrl,
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

                    // Đối với thông báo liên quan đến TradeTransaction: Chỉ gửi link tệp PDF trên Supabase Storage CDN cho Người mua (người thắng) và Người bán
                    if (referenceType == "TradeTransaction" && referenceId.HasValue)
                    {
                        try
                        {
                            using var pdfScope = _serviceScopeFactory.CreateScope();
                            var tradeUow = pdfScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                            var trade = await tradeUow.TradeTransactions.GetByIdAsync(referenceId.Value, cancellationToken);

                            // Phân quyền: CHỈ Người mua hoặc Người bán mới nhận được link PDF tài liệu qua email
                            if (trade != null && (trade.BuyerId == userId || trade.SellerId == userId))
                            {
                                if (string.IsNullOrWhiteSpace(emailActionUrl) || !emailActionUrl.Contains(".supabase.co", StringComparison.OrdinalIgnoreCase))
                                {
                                    var pdfService = pdfScope.ServiceProvider.GetService<IPdfContractService>();
                                    if (pdfService != null)
                                    {
                                        var freshNoticeUrl = await pdfService.EnsureWinningNoticeUploadedAsync(referenceId.Value, cancellationToken);
                                        if (!string.IsNullOrWhiteSpace(freshNoticeUrl))
                                        {
                                            emailActionUrl = freshNoticeUrl;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // Không phải Người mua hoặc Người bán -> Xóa triệt để URL hành động trong email
                                emailActionUrl = null;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "[NotificationService] Không thể fallback link Supabase PDF cho email Trade {TradeId}", referenceId.Value);
                        }
                    }

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
