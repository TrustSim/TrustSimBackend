using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Interfaces;

namespace TrustSimBackend.Infrastructure.BackgroundServices;

/// <summary>
/// Background Service tự động quét kiểm toán toàn vẹn Sổ cái kép &amp; Hash Chain định kỳ.
/// </summary>
public class LedgerIntegrityCheckWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LedgerIntegrityCheckWorker> _logger;

    // Chu kỳ quét định kỳ (mặc định: mỗi 10 phút)
    private const int CheckIntervalMinutes = 10;

    public LedgerIntegrityCheckWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<LedgerIntegrityCheckWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "[Ledger Integrity Worker] Background verifier started. Auditing every {interval} minutes.",
            CheckIntervalMinutes);

        // Chờ 30 giây sau khi khởi động app mới chạy lần đầu tiên
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var verifier = scope.ServiceProvider.GetRequiredService<ILedgerIntegrityVerifier>();

                var report = await verifier.GenerateFullAuditReportAsync(stoppingToken);

                if (!report.IsChainValid || !report.IsSignatureValid || !report.IsBalanceReconciled)
                {
                    _logger.LogCritical(
                        "[Ledger Integrity Worker] 🚨 BÁO ĐỘNG ĐỎ: Phát hiện bất thường toàn vẹn tài chính! ChainValid={Chain}, SigValid={Sig}, BalanceReconciled={Bal}, Anomalies={Count}",
                        report.IsChainValid, report.IsSignatureValid, report.IsBalanceReconciled, report.Anomalies.Count);
                }
                else
                {
                    _logger.LogInformation(
                        "[Ledger Integrity Worker] ✅ Kiểm toán tài chính HOÀN HẢO: {Total} mắt xích toàn vẹn, số dư toàn sàn khớp 100% ({Time}ms).",
                        report.TotalEntriesScanned, report.ExecutionTimeMs);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[Ledger Integrity Worker] Lỗi xảy ra trong quá trình quét kiểm toán sổ cái định kỳ.");
            }

            await Task.Delay(TimeSpan.FromMinutes(CheckIntervalMinutes), stoppingToken);
        }

        _logger.LogInformation("[Ledger Integrity Worker] Background verifier stopped.");
    }
}
