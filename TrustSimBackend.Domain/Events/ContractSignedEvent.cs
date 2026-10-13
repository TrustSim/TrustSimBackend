using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Events;

/// <summary>
/// Bắn ra khi một bên (Người Mua hoặc Người Bán) hoàn tất xác thực OTP ký hợp đồng.
/// </summary>
public record ContractSignedEvent(
    Guid ContractId, 
    Guid TradeTransactionId, 
    Guid UserId, 
    SignatureRole Role) : BaseDomainEvent;
