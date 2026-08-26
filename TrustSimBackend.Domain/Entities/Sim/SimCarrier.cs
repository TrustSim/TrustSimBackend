using TrustSimBackend.Domain.Base;

namespace TrustSimBackend.Domain.Entities.Sim;

/// <summary>
/// Đại diện cho một nhà mạng viễn thông (Viettel, Vinaphone, Mobifone...).
/// Quản lý danh sách các đầu số (<see cref="PhonePrefix"/>) thuộc nhà mạng đó.
/// </summary>
public class SimCarrier : FullAuditableEntity
{
    /// <summary>Khóa chính của nhà mạng (alias cho <see cref="BaseEntity.Id"/>).</summary>
    public Guid SimCarrierId { get => Id; private set => Id = value; }

    /// <summary>Tên đầy đủ của nhà mạng (VD: "Viettel Telecom").</summary>
    public string CarrierName { get; set; } = string.Empty;

    /// <summary>Mã định danh ngắn của nhà mạng dùng trong hệ thống (VD: "VTT", "VNP", "MBF").</summary>
    public string CarrierCode { get; set; } = string.Empty;

    /// <summary>URL logo của nhà mạng để hiển thị trên giao diện.</summary>
    public string LogoImg { get; set; } = string.Empty;

    // --- Navigation Property ---
    /// <summary>Danh sách các đầu số điện thoại thuộc nhà mạng này (quan hệ 1-N).</summary>
    public virtual ICollection<PhonePrefix> PhonePrefixes { get; set; } = new List<PhonePrefix>();
}