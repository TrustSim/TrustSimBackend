namespace TrustSimBackend.Domain.Enums
{
    public enum SimStatus
    {
        UNVERIFIED,      // Chưa xác minh danh tính/chính chủ
        AVAILABLE,       // Sẵn sàng (đã KYC)
        LISTED,
        IN_AUCTION, 
        TRANSFERRING,    // Sửa typo: TRANFERING -> TRANSFERRING
        TRANSFERRED,     // Sửa typo: TRANFERED -> TRANSFERRED
        SUSPENDED        // Sửa typo: SUSPENED -> SUSPENDED
    }
}