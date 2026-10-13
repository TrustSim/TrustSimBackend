using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TrustSimBackend.Application.Common.Interfaces;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Triển khai dịch vụ đẩy tác vụ ký số PAdES vào Redis Stream "stream:contracts:sign".
/// </summary>
public class ContractSigningQueueService : IContractSigningQueueService
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<ContractSigningQueueService> _logger;
    private const string StreamKey = "stream:contracts:sign";

    public ContractSigningQueueService(
        ILogger<ContractSigningQueueService> logger,
        IConfiguration configuration,
        IConnectionMultiplexer? redis = null)
    {
        _logger = logger;
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
                    _logger.LogWarning(ex, "[ContractSigningQueue] Không thể khởi tạo kết nối Redis Stream từ cấu hình.");
                }
            }
        }
    }

    public async Task EnqueueContractSigningTaskAsync(Guid tradeId, bool isAutoEnforced = false, CancellationToken cancellationToken = default)
    {
        if (_redis == null)
        {
            _logger.LogWarning("[ContractSigningQueue] Redis chưa được khởi tạo. Không thể đẩy task stream cho Trade {TradeId}", tradeId);
            return;
        }

        var db = _redis.GetDatabase();

        var entryId = await db.StreamAddAsync(StreamKey, new NameValueEntry[]
        {
            new("tradeId", tradeId.ToString()),
            new("isAutoEnforced", isAutoEnforced ? "true" : "false")
        });

        _logger.LogInformation(
            "[ContractSigningQueue] Đã đẩy tác vụ ký số cho Trade {TradeId} vào Redis Stream '{StreamKey}' (EntryId: {EntryId}, IsAutoEnforced: {IsAuto})",
            tradeId, StreamKey, entryId, isAutoEnforced);
    }
}
