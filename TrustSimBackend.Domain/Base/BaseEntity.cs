namespace TrustSimBackend.Domain.Base
{
    public abstract class BaseEntity : ISoftDelete, IAuditlog
    {
        public Guid Id { get; set; }

        // Implement ISoftDelete
        public DateTime? DeletedDate { get; set; }
        public string? DeletedBy { get; set; }
        public bool IsDeleted { get; set; }

        // Implement IAuditlog
        public DateTime CreatedDate { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedDate { get; set; }
        public string? UpdatedBy { get; set; }
    }
}