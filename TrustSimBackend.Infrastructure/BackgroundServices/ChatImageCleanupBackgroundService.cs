using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Infrastructure.BackgroundServices;

/// <summary>
/// Tiến trình chạy ngầm định kỳ quét và tự động xóa giải phóng các hình ảnh trong Chat đã vượt quá thời hạn lưu trữ
/// (mặc định: 7 ngày / 1 tuần theo cấu hình Minio:ChatImageExpirationDays trong appsettings.json).
/// </summary>
public class ChatImageCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChatImageCleanupBackgroundService> _logger;
    private readonly MinioSettings _minioSettings;

    public ChatImageCleanupBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ChatImageCleanupBackgroundService> logger,
        IOptions<MinioSettings> minioOptions)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _minioSettings = minioOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = _minioSettings.ChatImageCleanupIntervalMinutes > 0
            ? _minioSettings.ChatImageCleanupIntervalMinutes
            : 60;

        _logger.LogInformation(
            "[ChatImageCleanupWorker] Khởi động dịch vụ tự động giải phóng ảnh Chat quá hạn ({Days} ngày) mỗi {Minutes} phút.",
            _minioSettings.ChatImageExpirationDays, intervalMinutes);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var storageService = scope.ServiceProvider.GetRequiredService<ICloudStorageService>();
                await storageService.CleanupExpiredChatImagesAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "[ChatImageCleanupWorker] Lỗi khi thực thi dọn dẹp ảnh Chat quá hạn.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
