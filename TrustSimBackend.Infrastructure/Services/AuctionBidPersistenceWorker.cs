using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Infrastructure.Persistence;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Worker đồng bộ dữ liệu đặt giá bất đồng bộ (Write-Behind Persistence Engine).
/// Tiêu thụ message từ Redis Stream 'stream:auction:bids', gom batch mỗi 500ms hoặc khi đủ 50 bản ghi,
/// thực hiện Bulk Insert/Update vào SQL Server và commit XACK an toàn.
/// </summary>
public class AuctionBidPersistenceWorker : BackgroundService
{
    private const string StreamKey = "stream:auction:bids";
    private const string ConsumerGroupName = "group_persistence";
    private const string ConsumerName = "worker_1";
    private const int BatchSize = 50;
    private const int FlushIntervalMs = 500;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<AuctionBidPersistenceWorker> _logger;

    public AuctionBidPersistenceWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<AuctionBidPersistenceWorker> logger,
        IConnectionMultiplexer? redis = null)
    {
        _scopeFactory = scopeFactory;
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
                    _logger.LogWarning(ex, "[PersistenceWorker] Không thể kết nối Redis Stream.");
                }
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_redis == null || !_redis.IsConnected)
        {
            _logger.LogWarning("[PersistenceWorker] Redis không khả dụng. Worker tạm dừng.");
            return;
        }

        var db = _redis.GetDatabase();

        // 1. Tạo Consumer Group nếu chưa tồn tại
        try
        {
            await db.StreamCreateConsumerGroupAsync(StreamKey, ConsumerGroupName, "0-0", createStream: true);
            _logger.LogInformation("[PersistenceWorker] Đã khởi tạo Consumer Group '{Group}' trên Stream '{Stream}'", ConsumerGroupName, StreamKey);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Consumer group đã tồn tại, an toàn bỏ qua
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PersistenceWorker] Lỗi tạo Consumer Group.");
        }

        var batchBuffer = new List<(RedisValue MessageId, BidMessagePayload Payload)>();
        var lastFlushTime = DateTime.UtcNow;

        // 2. Vòng lặp tiêu thụ Micro-Batching
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Đọc tin nhắn mới từ Stream
                var entries = await db.StreamReadGroupAsync(
                    StreamKey,
                    ConsumerGroupName,
                    ConsumerName,
                    ">",
                    count: BatchSize);

                if (entries.Length > 0)
                {
                    foreach (var entry in entries)
                    {
                        var payloadField = entry.Values.FirstOrDefault(v => v.Name == "payload");
                        if (!payloadField.Value.IsNull)
                        {
                            var payloadJson = (string?)payloadField.Value;
                            if (!string.IsNullOrWhiteSpace(payloadJson))
                            {
                                try
                                {
                                    var item = JsonSerializer.Deserialize<BidMessagePayload>(payloadJson);
                                    if (item != null)
                                    {
                                        batchBuffer.Add((entry.Id, item));
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "[PersistenceWorker] Lỗi giải mã message {Id}", entry.Id);
                                }
                            }
                        }
                    }
                }

                var elapsedMs = (DateTime.UtcNow - lastFlushTime).TotalMilliseconds;

                // Điều kiện xả lô (Flush condition): Đủ 50 phần tử HOẶC Đã quá 500ms
                if (batchBuffer.Count >= BatchSize || (batchBuffer.Count > 0 && elapsedMs >= FlushIntervalMs))
                {
                    await FlushBatchToSqlDatabaseAsync(db, batchBuffer, stoppingToken);
                    batchBuffer.Clear();
                    lastFlushTime = DateTime.UtcNow;
                }
                else if (entries.Length == 0)
                {
                    // Hàng đợi trống, nghỉ 100ms trước khi thăm dò tiếp
                    await Task.Delay(100, stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[PersistenceWorker] Lỗi trong vòng lặp tiêu thụ Stream.");
                await Task.Delay(1000, stoppingToken);
            }
        }

        // Xả nốt các bản ghi còn sót lại trước khi tắt ứng dụng
        if (batchBuffer.Count > 0)
        {
            await FlushBatchToSqlDatabaseAsync(db, batchBuffer, CancellationToken.None);
        }
    }

    private async Task FlushBatchToSqlDatabaseAsync(
        IDatabase db,
        List<(RedisValue MessageId, BidMessagePayload Payload)> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0) return;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrustSimDbContext>();

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // 1. Chuẩn bị danh sách Bids để Bulk Insert
            var bidEntities = batch.Select(b => new Bid
            {
                Id = b.Payload.bidId,
                AuctionId = b.Payload.auctionId,
                ParticipantId = b.Payload.participantId,
                BidAmount = b.Payload.bidAmount,
                PlacedAt = DateTimeOffset.FromUnixTimeMilliseconds(b.Payload.placedAtMs).UtcDateTime,
                Status = BidStatus.ACCEPTED
            }).ToList();

            // 2. Nhóm theo AuctionId để cập nhật CurrentPrice, LockVersion, EndAt
            var auctionGroups = batch.GroupBy(b => b.Payload.auctionId);
            foreach (var ag in auctionGroups)
            {
                var auctionId = ag.Key;
                var maxBidItem = ag.MaxBy(x => x.Payload.bidAmount);
                var maxBid = maxBidItem.Payload;
                if (maxBid != null)
                {
                    var auction = await context.Auctions.FindAsync(new object[] { auctionId }, cancellationToken);
                    if (auction != null)
                    {
                        if (maxBid.bidAmount > auction.CurrentPrice)
                        {
                            auction.CurrentPrice = maxBid.bidAmount;
                        }
                        if (maxBid.lockVersion > auction.LockVersion)
                        {
                            auction.LockVersion = (int)maxBid.lockVersion;
                        }
                        if (maxBid.isSnipeExtended)
                        {
                            var newEndAt = DateTimeOffset.FromUnixTimeMilliseconds(maxBid.newEndAtMs).UtcDateTime;
                            if (newEndAt > auction.EndAt)
                            {
                                auction.EndAt = newEndAt;
                            }
                        }
                    }
                }
            }

            // 3. Cập nhật thống kê người chơi (AcceptedBidCount, SnipeCount)
            var participantGroups = batch.GroupBy(b => b.Payload.participantId);
            foreach (var pg in participantGroups)
            {
                var pId = pg.Key;
                var count = pg.Count();
                var snipeCount = pg.Count(x => x.Payload.isSnipeExtended);

                var participant = await context.AuctionParticipants.FindAsync(new object[] { pId }, cancellationToken);
                if (participant != null)
                {
                    participant.AcceptedBidCount += count;
                    participant.SnipeExtensionCount += snipeCount;
                }
            }

            await context.Bids.AddRangeAsync(bidEntities, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            // 4. Gửi XACK xác nhận các tin nhắn đã commit an toàn vào Cơ sở dữ liệu
            var messageIds = batch.Select(b => b.MessageId).ToArray();
            await db.StreamAcknowledgeAsync(StreamKey, ConsumerGroupName, messageIds);

            sw.Stop();
            _logger.LogInformation("[PersistenceWorker] ⚡ Đã ghi đĩa thành công {Count} bids trong {Time}ms.", batch.Count, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PersistenceWorker] Lỗi nghiêm trọng khi ghi lô {Count} bids vào CSDL. Đang chuyển sang DLQ.", batch.Count);
            // Đẩy vào Dead-Letter Queue (DLQ) để bảo toàn dữ liệu
            try
            {
                foreach (var item in batch)
                {
                    var dlqPayload = JsonSerializer.Serialize(item.Payload);
                    await db.StreamAddAsync("stream:auction:bids:dlq", "payload", dlqPayload);
                    await db.StreamAcknowledgeAsync(StreamKey, ConsumerGroupName, item.MessageId);
                }
            }
            catch { }
        }
    }

    public record BidMessagePayload(
        Guid bidId,
        Guid auctionId,
        Guid participantId,
        Guid userId,
        decimal bidAmount,
        long placedAtMs,
        bool isSnipeExtended,
        long newEndAtMs,
        long lockVersion);
}
