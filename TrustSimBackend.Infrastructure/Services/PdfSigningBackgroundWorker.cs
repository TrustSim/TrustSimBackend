using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Background Service tiêu thụ các task ký số hợp đồng bất đồng bộ từ Redis Stream "stream:contracts:sign".
/// Đảm bảo tính toàn vẹn với cơ chế Consumer Group và Acknowledgment (XACK).
/// </summary>
public class PdfSigningBackgroundWorker : BackgroundService
{
    private readonly ILogger<PdfSigningBackgroundWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConnectionMultiplexer? _redis;
    private const string StreamKey = "stream:contracts:sign";
    private const string GroupName = "group:pdf_signers";
    private const string ConsumerName = "worker-1";

    public PdfSigningBackgroundWorker(
        ILogger<PdfSigningBackgroundWorker> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        IConnectionMultiplexer? redis = null)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _redis = redis;

        if (_redis == null)
        {
            var redisConnection = configuration.GetConnectionString("Redis") ?? configuration["Redis:ConnectionString"];
            if (!string.IsNullOrWhiteSpace(redisConnection))
            {
                try
                {
                    var configOptions = ConfigurationOptions.Parse(redisConnection);
                    configOptions.AbortOnConnectFail = false;
                    configOptions.ConnectTimeout = 10000;
                    configOptions.SyncTimeout = 10000;
                    configOptions.KeepAlive = 60;
                    _redis = ConnectionMultiplexer.Connect(configOptions);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[PdfSigningWorker] Không thể kết nối Redis Stream từ cấu hình.");
                }
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_redis == null)
        {
            _logger.LogWarning("[PdfSigningWorker] Redis chưa được khởi tạo. Worker sẽ ở trạng thái chờ.");
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) { }
            return;
        }

        var db = _redis.GetDatabase();

        // Tạo Consumer Group nếu chưa tồn tại
        try
        {
            await db.StreamCreateConsumerGroupAsync(StreamKey, GroupName, StreamPosition.NewMessages);
        }
        catch { /* Group đã tồn tại */ }

        _logger.LogInformation("[PdfSigningWorker] Khởi động bộ tiêu thụ Redis Stream '{Key}'...", StreamKey);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var entries = await db.StreamReadGroupAsync(StreamKey, GroupName, ConsumerName, ">", count: 5);
                if (entries == null || entries.Length == 0)
                {
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }

                foreach (var entry in entries)
                {
                    var tradeIdStr = entry.Values.FirstOrDefault(v => v.Name == "tradeId").Value.ToString();
                    var isAutoEnforcedStr = entry.Values.FirstOrDefault(v => v.Name == "isAutoEnforced").Value.ToString();
                    bool.TryParse(isAutoEnforcedStr, out var isAutoEnforced);

                    if (Guid.TryParse(tradeIdStr, out var tradeId))
                    {
                        await ProcessContractSigningAsync(tradeId, isAutoEnforced, stoppingToken);
                    }

                    // Acknowledge (XACK) thông điệp
                    await db.StreamAcknowledgeAsync(StreamKey, GroupName, entry.Id);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "[PdfSigningWorker] Lỗi tiêu thụ task từ Redis Stream.");
                await Task.Delay(2000, stoppingToken);
            }
        }
    }

    private async Task ProcessContractSigningAsync(Guid tradeId, bool isAutoEnforced, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var pdfContractService = scope.ServiceProvider.GetRequiredService<IPdfContractService>();
        var pdfSigner = scope.ServiceProvider.GetRequiredService<IPdfSigningService>();
        var cloudStorage = scope.ServiceProvider.GetRequiredService<ICloudStorageService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var trade = await unitOfWork.TradeTransactions.GetByIdAsync(tradeId, ct);
        if (trade == null) return;

        // Đảm bảo TransferContract tồn tại
        if (trade.TransferContract == null)
        {
            var newContract = new TransferContract
            {
                Id = Guid.NewGuid(),
                TradeTransactionId = trade.Id,
                ContractStatus = ContractStatus.DRAFT
            };
            await unitOfWork.TradeTransactions.AddTransferContractAsync(newContract, ct);
            trade.TransferContract = newContract;
        }

        _logger.LogInformation("[PdfSigningWorker] Bắt đầu sinh và ký số Hợp đồng cho giao dịch {TradeCode}...", trade.TradeTransactionCode);

        // 1. Sinh bản byte PDF từ template
        var rawPdfBytes = await pdfContractService.GenerateRawPdfBytesAsync(trade.Id, isAutoEnforced, ct);

        // 2. Lấy thông tin hộp dấu Audit
        var stampBoxes = await pdfContractService.GetStampBoxesAsync(trade.Id, isAutoEnforced, ct);

        // 3. Ký số PAdES X.509 + TSA
        var signResult = await pdfSigner.SignPdfPadesAsync(
            rawPdfBytes, 
            "Xác nhận chuyển nhượng quyền sử dụng số thuê bao viễn thông", 
            "Hà Nội, Việt Nam", 
            stampBoxes, 
            ct);

        // 4. Upload lên S3 / MinIO
        var fileName = $"{trade.TradeTransactionCode}_Signed.pdf";
        var fileUrl = await cloudStorage.UploadImageBytesAsync(signResult.SignedPdfBytes, fileName, "contracts", "application/pdf", ct);

        // 5. Cập nhật Database
        trade.TransferContract.DocumentUrl = fileUrl;
        trade.TransferContract.DocumentHash = signResult.Sha256Hash;
        trade.TransferContract.PadesSignatureSerial = signResult.CertificateSerial;
        trade.TransferContract.PadesSignatureHash = signResult.Sha256Hash;
        trade.TransferContract.TsaTimestamp = signResult.TsaTimestamp;
        trade.TransferContract.TsaAuthority = signResult.TsaAuthority;
        trade.TransferContract.IsAutoEnforced = isAutoEnforced;
        trade.TransferContract.SealedAt = signResult.SignedAt;
        trade.TransferContract.ContractStatus = ContractStatus.FULLY_SIGNED;
        
        // Chuyển TradeTransaction sang PendingTransfer
        trade.TradeTransactionStatus = TradeTransactionStatus.PendingTransfer;

        await unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("[PdfSigningWorker] ✅ Niêm phong PAdES thành công: {Url} (Hash: {Hash})", fileUrl, signResult.Sha256Hash);

        // Bắn thông báo cho cả 2 bên
        await notificationService.SendNotificationAsync(
            trade.BuyerId,
            NotificationType.CONTRACT_FULLY_SIGNED,
            "Hợp đồng đã được ký số niêm phong",
            $"Hợp đồng chuyển nhượng SIM {trade.SimNumber?.PhoneNumber} đã được hoàn tất ký số. Vui lòng theo dõi tiến trình sang tên.",
            $"/trade/{trade.Id}",
            cancellationToken: ct);

        await notificationService.SendNotificationAsync(
            trade.SellerId,
            NotificationType.CONTRACT_FULLY_SIGNED,
            "Hợp đồng đã được ký số niêm phong",
            $"Hợp đồng chuyển nhượng SIM {trade.SimNumber?.PhoneNumber} đã được niêm phong. Vui lòng tiến hành sang tên chính chủ cho người mua.",
            $"/trade/{trade.Id}",
            cancellationToken: ct);
    }
}
