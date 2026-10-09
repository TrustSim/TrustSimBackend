using TrustSimBackend.Domain.Base;

namespace TrustSimBackend.Domain.Events;

/// <summary>
/// Sự kiện Domain kích hoạt khi Winner bùng kèo, hệ thống bồi thường cho Người bán và mời Người bán duyệt bán cho Á quân.
/// </summary>
public record SellerRunnerUpApprovalRequestedEvent(
    Guid AuctionId,
    Guid SellerId,
    Guid RunnerUpParticipantId,
    decimal RunnerUpPrice,
    decimal CompensationAmount,
    DateTime DecisionDeadline) : BaseDomainEvent;
