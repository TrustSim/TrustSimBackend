namespace TrustSimBackend.Domain.Enums
{
    public enum EscrowStatus
    {
        PENDING_FUNDING,
        HELD,
        FROZEN,
        RELEASED,
        REFUNDED,
        CANCELLED
    }
}