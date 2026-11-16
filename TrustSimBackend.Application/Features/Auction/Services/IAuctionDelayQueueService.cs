namespace TrustSimBackend.Application.Features.Auction.Services;

/// <summary>
/// Định nghĩa các loại tác vụ trễ trong vòng đời đấu giá.
/// </summary>
public static class AuctionDelayJobTypes
{
    public const string AUCTION_PRE_WARM = "AUCTION_PRE_WARM";
    public const string AUCTION_START = "AUCTION_START";
    public const string AUCTION_END = "AUCTION_END";
    public const string DUTCH_PRICE_DROP = "DUTCH_PRICE_DROP";
    public const string PAYMENT_DEADLINE = "PAYMENT_DEADLINE";
    public const string RUNNER_UP_DEADLINE = "RUNNER_UP_DEADLINE";
    public const string SELLER_DECISION_DEADLINE = "SELLER_DECISION_DEADLINE";
    public const string SIGNING_DEADLINE = "SIGNING_DEADLINE";
    public const string TRANSFER_DEADLINE = "TRANSFER_DEADLINE";
    public const string LIQUIDATION_DEADLINE = "LIQUIDATION_DEADLINE";

    /// <summary>
    /// Phân luồng hàng đợi: phiên live cần độ trễ cực thấp tách riêng khỏi các deadline hậu đấu giá.
    /// </summary>
    public static AuctionDelayQueueLane GetLane(string? jobType) => jobType switch
    {
        AUCTION_PRE_WARM or AUCTION_START or DUTCH_PRICE_DROP or AUCTION_END => AuctionDelayQueueLane.Live,
        _ => AuctionDelayQueueLane.PostAuction
    };
}

/// <summary>
/// Phân loại hàng đợi theo độ ưu tiên thời gian thực.
/// </summary>
public enum AuctionDelayQueueLane
{
    Live = 0,
    PostAuction = 1
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
    /// Đẩy một tác vụ trễ vào hàng đợi Redis ZSET với Score = ExecuteAt (Unix epoch ms) và phát tín hiệu đánh thức worker tương ứng.
    /// </summary>
    Task EnqueueJobAsync(AuctionDelayJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đẩy tác vụ trễ vào hàng đợi nhưng giữ nguyên job cùng loại đang tồn tại nếu job đó có ExecuteAt muộn hơn (tránh ghi đè lùi Anti-Sniping).
    /// </summary>
    Task EnqueueJobPreservingLaterAsync(AuctionDelayJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hủy một hoặc nhiều tác vụ của một phiên đấu giá (theo JobType hoặc tất cả).
    /// </summary>
    Task CancelJobAsync(Guid auctionId, string? jobType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy và xóa (atomic pop) một tác vụ đã đến hạn thực thi (ExecuteAt &lt;= now) theo từng lane.
    /// </summary>
    Task<AuctionDelayJob?> PopNextDueJobAsync(
        DateTime now,
        AuctionDelayQueueLane lane = AuctionDelayQueueLane.Live,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy thời điểm thực thi của tác vụ sớm nhất tiếp theo theo từng lane để cài đặt In-Memory Timer.
    /// </summary>
    Task<DateTime?> GetNextJobTimeAsync(
        AuctionDelayQueueLane lane = AuctionDelayQueueLane.Live,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chờ tối đa <paramref name="delay"/> hoặc thức dậy ngay lập tức khi có tác vụ mới được đưa vào lane.
    /// </summary>
    Task WaitForSignalOrDelayAsync(
        AuctionDelayQueueLane lane,
        TimeSpan delay,
        CancellationToken cancellationToken = default);
}
