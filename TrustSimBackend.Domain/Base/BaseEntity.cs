using TrustSimBackend.Domain.Base;

namespace TrustSimBackend.Domain.Base
{
    /// <summary>
    /// Lớp cơ sở tối thiểu: chỉ có Id, IsActive và Domain Events collection.
    /// Tất cả Entity đều kế thừa từ đây.
    /// </summary>
    public abstract class BaseEntity
    {
        public Guid Id { get; set; }
        public bool IsActive { get; set; } = true;

        // --- Domain Events ---
        // Dùng private field + IReadOnlyCollection để đảm bảo chỉ có Entity tự raise event
        // (bên ngoài không thể Add/Remove trực tiếp vào list)
        private readonly List<IDomainEvent> _domainEvents = new();

        public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        /// <summary>Thêm một Domain Event vào hàng đợi của Entity này.</summary>
        protected void AddDomainEvent(IDomainEvent domainEvent)
            => _domainEvents.Add(domainEvent);

        /// <summary>Xóa một Domain Event cụ thể khỏi hàng đợi.</summary>
        public void RemoveDomainEvent(IDomainEvent domainEvent)
            => _domainEvents.Remove(domainEvent);

        /// <summary>
        /// Xóa toàn bộ hàng đợi event.
        /// Được gọi bởi DbContext sau khi đã Dispatch tất cả event.
        /// </summary>
        public void ClearDomainEvents()
            => _domainEvents.Clear();
    }
}