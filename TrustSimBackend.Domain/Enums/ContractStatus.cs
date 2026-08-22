namespace TrustSimBackend.Domain.Enums
{
    public enum ContractStatus
    {
        DRAFT,
        PENDING_SIGNATURE,
        PARTIALLY_SIGNED,
        FULLY_SIGNED,
        OFFLINE_PROCESSING,
        PENDING_VERIFICATION,
        COMPLETED,
        DISPUTED
    }
}