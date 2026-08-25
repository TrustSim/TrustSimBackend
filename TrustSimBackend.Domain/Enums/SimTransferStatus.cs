namespace TrustSimBackend.Domain.Enums
{
    public enum SimTransferStatus
    {
        PENDING_SELLER_ACTION,
        SELLER_PROOF_SUBMITTED,
        PENDING_BUYER_ACTION,
        BUYER_PROOF_SUBMITTED,
        COMPLETED,
        CANCELLED
    }
}