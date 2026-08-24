namespace TrustSimBackend.Domain.Base
{
    /// <summary>
    /// Lớp cơ sở tối thiểu: chỉ có Id và IsActive.
    /// Tất cả Entity đều kế thừa từ đây.
    /// </summary>
    public abstract class BaseEntity
    {
        public Guid Id { get; set; }
        public bool IsActive { get; set; } = true;
    }
}