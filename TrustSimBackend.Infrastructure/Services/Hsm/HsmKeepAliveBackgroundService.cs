using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Infrastructure.Services.Hsm;

/// <summary>
/// Tiến trình nền định kỳ ping endpoint /health của SoftHSMv2 microservice mỗi 5 phút
/// để loại bỏ trạng thái Sleep / Cold-Start khi triển khai trên hạ tầng PaaS Cloud (Render Free Tier).
/// </summary>
public class HsmKeepAliveBackgroundService : BackgroundService
{
    private readonly ISoftHsmClient _hsmClient;
    private readonly SoftHsmSettings _settings;
    private readonly ILogger<HsmKeepAliveBackgroundService> _logger;

    private static readonly TimeSpan PingInterval = TimeSpan.FromMinutes(5);

    public HsmKeepAliveBackgroundService(
        ISoftHsmClient hsmClient,
        IOptions<SoftHsmSettings> settings,
        ILogger<HsmKeepAliveBackgroundService> logger)
    {
        _hsmClient = hsmClient;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.EnableRemoteHsm || !_settings.EnableKeepAlive)
        {
            _logger.LogInformation("[SoftHSM KeepAlive] Tính năng Keep-Alive đã bị vô hiệu hóa trong cấu hình.");
            return;
        }

        _logger.LogInformation("[SoftHSM KeepAlive] Khởi động tiến trình giữ nhiệt SoftHSM định kỳ mỗi 5 phút.");

        using var timer = new PeriodicTimer(PingInterval);

        // Ping ngay lần đầu khi container khởi động để đánh thức SoftHSM
        await PingHsmAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PingHsmAsync(stoppingToken);
        }
    }

    private async Task PingHsmAsync(CancellationToken cancellationToken)
    {
        try
        {
            var health = await _hsmClient.CheckHealthAsync(cancellationToken);
            _logger.LogDebug("[SoftHSM KeepAlive] Ping thành công: Status={Status}, Token={Token}, Slot={Slot}",
                health.Status, health.TokenLabel, health.SlotId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[SoftHSM KeepAlive] Ping SoftHSM thất bại: {Message}", ex.Message);
        }
    }
}
