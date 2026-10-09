using TrustSimBackend.Domain.Base;

namespace TrustSimBackend.Domain.Events;

/// <summary>
/// Sự kiện Domain kích hoạt khi phát hiện dấu hiệu Lá chắn giá / Thổi giá bất thường (Bid Shielding).
/// </summary>
public record BidShieldingDetectedEvent(
    Guid AuctionId,
    decimal WinnerPrice,
    decimal RunnerUpPrice,
    decimal GapRatio,
    int UniqueBiddersCount,
    string Reason) : BaseDomainEvent;
