using TrustSimBackend.Domain.Base;

namespace TrustSimBackend.Domain.Events;

// Bắn ra khi TradeTransaction đổi trạng thái sang Completed -> Báo hiệu kế toán giải ngân cho Seller
public record TradeTransactionCompletedEvent(Guid TradeTransactionId, Guid SellerId) : BaseDomainEvent;
