using TrustSimBackend.Domain.Base;

namespace TrustSimBackend.Domain.Entities.Sim;

/// <summary>
/// Thực thể kết hợp (Junction Entity) ánh xạ quan hệ N-N giữa <see cref="SimNumber"/> và <see cref="SimCategory"/>.
/// Một số SIM có thể thuộc nhiều danh mục, mỗi danh mục đóng góp một điểm số (<see cref="ScoreContribution"/>)
/// vào điểm định giá tổng thể của SIM.
/// </summary>
public class SimCategoryMapping : AuditableEntity
{
    /// <summary>Khóa chính của bản ghi ánh xạ (alias cho <see cref="BaseEntity.Id"/>).</summary>
    public Guid SimCategoryMappingId { get => Id; private set => Id = value; }

    /// <summary>
    /// Điểm đóng góp của danh mục này vào điểm định giá tổng của số SIM.
    /// Cho phép cùng một danh mục nhưng đóng góp khác nhau tùy số SIM cụ thể.
    /// </summary>
    public float ScoreContribution { get; set; } = 1.0f;

    // --- Foreign Keys ---
    /// <summary>ID của số SIM được phân vào danh mục này.</summary>
    public Guid SimNumberId { get; set; }

    /// <summary>ID của danh mục mà số SIM được phân vào.</summary>
    public Guid SimCategoryId { get; set; }

    // --- Navigation Properties ---
    /// <summary>Số SIM tham chiếu trong bản ghi ánh xạ này.</summary>
    public virtual SimNumber? SimNumber { get; set; }

    /// <summary>Danh mục tham chiếu trong bản ghi ánh xạ này.</summary>
    public virtual SimCategory? SimCategory { get; set; }
}
