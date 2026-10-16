namespace TrustSimBackend.Application.Features.Auction.Services;

/// <summary>
/// Định nghĩa các loại tác vụ trễ trong vòng đời đấu giá.
/// </summary>
public static class AuctionDelayJobTypes
{
    public const string AUCTION_START = "AUCTION_START";
    public const string AUCTION_END = "AUCTION_END";
    public const string DUTCH_PRICE_DROP = "DUTCH_PRICE_DROP";
    public const string PAYMENT_DEADLINE = "PAYMENT_DEADLINE";
    public const string RUNNER_UP_DEADLINE = "RUNNER_UP_DEADLINE";
    public const string SELLER_DECISION_DEADLINE = "SELLER_DECISION_DEADLINE";
    public const string SIGNING_DEADLINE = "SIGNING_DEADLINE";
    public const string TRANSFER_DEADLINE = "TRANSFER_DEADLINE";
    public const string LIQUIDATION_DEADLINE = "LIQUIDATION_DEADLINE";
}

/// <summary>
/// Mô tả dữ liệu của một tác vụ trễ được lưu trữ trong Redis ZSET.
/// </summary>
public class AuctionDelayJob
{
    public Guid JobId { get; set; } = Guid.NewGuid();
    public Guid AuctionId { get; set; }
    public string JobType { get; set; } = string.Empty;
    public DateTime ExecuteAt { get; set; }
    public string? ExtraData { get; set; }
}

/// <summary>
/// Interface dịch vụ quản lý hàng đợi trễ phân tán (Redis ZSET Delay Queue).
/// </summary>
public interface IAuctionDelayQueueService
{
    /// <summary>
    /// Đẩy một tác vụ trễ vào hàng đợi Redis ZSET với Score = ExecuteAt (Unix epoch ms).
    /// </summary>
    Task EnqueueJobAsync(AuctionDelayJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hủy một hoặc nhiều tác vụ của một phiên đấu giá (theo JobType hoặc tất cả).
    /// </summary>
    Task CancelJobAsync(Guid auctionId, string? jobType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy và xóa (atomic pop) một tác vụ đã đến hạn thực thi (ExecuteAt &lt;= now).
    /// </summary>
    Task<AuctionDelayJob?> PopNextDueJobAsync(DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy thời điểm thực thi của tác vụ sớm nhất tiếp theo để cài đặt In-Memory Timer.
    /// </summary>
    Task<DateTime?> GetNextJobTimeAsync(CancellationToken cancellationToken = default);
}
