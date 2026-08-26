using TrustSimBackend.Domain.Base;

namespace TrustSimBackend.Domain.Events;

// Bắn ra khi người mua không nộp tiền ký quỹ đúng hạn -> Báo hiệu hủy giao dịch
public record EscrowTimeoutEvent(Guid TradeTransactionId, Guid BuyerId) : BaseDomainEvent;
