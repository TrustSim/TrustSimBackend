namespace TrustSimBackend.Domain.Base
{
    /// <summary>
    /// Entity đầy đủ: Audit + Xóa mềm (Soft Delete).
    /// Dùng cho: UserAccount, UserProfile, SimNumber, SimCarrier, Auction...
    /// </summary>
    public abstract class FullAuditableEntity : AuditableEntity, ISoftDelete
    {
        // Implement ISoftDelete
        public bool IsDeleted { get; set; }
        public DateTime? DeletedDate { get; set; }
        public string? DeletedBy { get; set; }
    }
}
