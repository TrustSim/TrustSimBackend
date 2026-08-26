using TrustSimBackend.Domain.Base;

namespace TrustSimBackend.Domain.Entities.Sim;

/// <summary>
/// Danh mục phân loại số SIM theo đặc tính phong thủy / số học (VD: Thần tài, Tứ quý, Ngũ quý, Lục quý...).
/// Một số SIM có thể thuộc nhiều danh mục cùng lúc (quan hệ N-N thông qua <see cref="SimCategoryMapping"/>).
/// Hệ số <see cref="ValuationMultiplier"/> ảnh hưởng đến giá ước tính tổng thể của SIM.
/// </summary>
public class SimCategory : FullAuditableEntity
{
    /// <summary>Khóa chính của danh mục (alias cho <see cref="BaseEntity.Id"/>).</summary>
    public Guid SimCategoryId { get => Id; private set => Id = value; }

    /// <summary>Mã định danh ngắn của danh mục dùng trong hệ thống (VD: "THAN_TAI", "TU_QUY").</summary>
    public string CategoryCode { get; set; } = string.Empty;

    /// <summary>Tên hiển thị của danh mục (VD: "SIM Thần tài", "SIM Tứ quý 8888").</summary>
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>Mô tả chi tiết về tiêu chí phân loại của danh mục này.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Hệ số nhân định giá của danh mục. Được dùng trong công thức tính giá ước tính.
    /// VD: 1.5 nghĩa là SIM trong danh mục này được định giá cao hơn 50% so với SIM thường.
    /// </summary>
    public float ValuationMultiplier { get; set; } = 1.0f;

    // --- Navigation Property ---
    /// <summary>Danh sách ánh xạ liên kết danh mục này với các số SIM thuộc về nó (quan hệ N-N).</summary>
    public virtual ICollection<SimCategoryMapping> SimCategoryMappings { get; set; } = new List<SimCategoryMapping>();
}