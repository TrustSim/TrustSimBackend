namespace TrustSimBackend.Domain.Base
{
    /// <summary>
    /// Entity có thể được Audit (ghi nhận người tạo, người sửa, thời điểm tạo/sửa).
    /// Dùng cho: các bản ghi KHÔNG BAO GIỜ bị xóa (Verification, SecurityProfile, Log...).
    /// </summary>
    public abstract class AuditableEntity : BaseEntity, IAuditlog
    {
        // Implement IAuditlog
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public DateTime UpdatedDate { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
