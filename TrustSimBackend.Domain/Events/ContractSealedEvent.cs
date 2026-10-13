using TrustSimBackend.Domain.Base;

namespace TrustSimBackend.Domain.Events;

/// <summary>
/// Bắn ra khi Sàn hoàn tất ký số PAdES niêm phong hợp đồng chuyển nhượng SIM.
/// </summary>
public record ContractSealedEvent(
    Guid ContractId, 
    Guid TradeTransactionId, 
    string DocumentUrl, 
    bool IsAutoEnforced) : BaseDomainEvent;
