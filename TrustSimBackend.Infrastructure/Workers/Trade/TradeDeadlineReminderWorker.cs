using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Infrastructure.Workers.Trade;

/// <summary>
/// Background Service chạy định kỳ để quét và phát thông báo nhắc hạn hậu ký hợp đồng số 2.
/// </summary>
public sealed class TradeDeadlineReminderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TradeDeadlineSettings _deadlineSettings;
    private readonly ILogger<TradeDeadlineReminderWorker> _logger;

    public TradeDeadlineReminderWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<TradeDeadlineSettings> deadlineSettingsOptions,
        ILogger<TradeDeadlineReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _deadlineSettings = deadlineSettingsOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = _deadlineSettings.ReminderCheckIntervalMinutes > 0
            ? _deadlineSettings.ReminderCheckIntervalMinutes
            : 15;

        _logger.LogInformation(
            "[TradeDeadlineReminderWorker] Khởi động quét nhắc hạn định kỳ mỗi {Minutes} phút.",
            intervalMinutes);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        await SafeRunTickAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SafeRunTickAsync(stoppingToken);
        }

        _logger.LogInformation("[TradeDeadlineReminderWorker] Đã dừng worker nhắc hạn định kỳ.");
    }

    private async Task SafeRunTickAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var reminderService = scope.ServiceProvider.GetRequiredService<ITradeDeadlineReminderService>();
            await reminderService.ProcessDueRemindersAsync(DateTime.UtcNow, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // App đang shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TradeDeadlineReminderWorker] Lỗi khi quét nhắc hạn giao dịch định kỳ.");
        }
    }
}
