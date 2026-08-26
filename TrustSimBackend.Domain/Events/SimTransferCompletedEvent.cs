using TrustSimBackend.Domain.Base;

namespace TrustSimBackend.Domain.Events;

// Bắn ra khi Admin duyệt ảnh người mua thành công -> Báo hiệu SimTransfer hoàn tất
public record SimTransferCompletedEvent(Guid TradeTransactionId) : BaseDomainEvent;
