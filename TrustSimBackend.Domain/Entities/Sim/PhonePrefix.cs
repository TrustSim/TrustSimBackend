using TrustSimBackend.Domain.Base;

namespace TrustSimBackend.Domain.Entities.Sim;

/// <summary>
/// Đại diện cho một đầu số điện thoại thuộc về một nhà mạng (VD: "090", "098" của Mobifone).
/// Mỗi đầu số thuộc về đúng một nhà mạng (<see cref="SimCarrier"/>) và có thể có nhiều số SIM.
/// </summary>
public class PhonePrefix : FullAuditableEntity
{
    /// <summary>Khóa chính của đầu số (alias cho <see cref="BaseEntity.Id"/>).</summary>
    public Guid PhonePrefixId { get => Id; private set => Id = value; }

    /// <summary>Giá trị chuỗi đầu số điện thoại (VD: "090", "0356", "058").</summary>
    public string PrefixValue { get; set; } = string.Empty;

    // --- Foreign Key ---
    /// <summary>ID của nhà mạng sở hữu đầu số này.</summary>
    public Guid SimCarrierId { get; set; }

    // --- Navigation Properties ---
    /// <summary>Nhà mạng sở hữu đầu số này (quan hệ N-1).</summary>
    public virtual SimCarrier? SimCarrier { get; set; }

    /// <summary>Danh sách các số SIM có đầu số này (quan hệ 1-N).</summary>
    public virtual ICollection<SimNumber> SimNumbers { get; set; } = new List<SimNumber>();
}