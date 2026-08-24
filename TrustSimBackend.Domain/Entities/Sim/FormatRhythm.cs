using TrustSimBackend.Domain.Base;

namespace TrustSimBackend.Domain.Entities.Sim;

/// <summary>
/// Định nghĩa các kiểu "nhịp số" (Format Rhythm) dùng để phân loại và định giá số SIM.
/// VD: "Sảnh" (0901234567), "Lặp đôi" (090990 9090), "Gánh" (0909 09090), "Ngũ quý" (090 99999)...
/// Mỗi <see cref="SimNumber"/> được gắn với một <see cref="FormatRhythm"/> duy nhất.
/// </summary>
public class FormatRhythm : FullAuditableEntity
{
    /// <summary>Khóa chính của kiểu định dạng (alias cho <see cref="BaseEntity.Id"/>).</summary>
    public Guid FormatRhythmId { get => Id; private set => Id = value; }

    /// <summary>Giá trị mẫu minh họa cho kiểu định dạng này (VD: "ABCDE" cho SIM sảnh 5 số).</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Tên hiển thị của kiểu định dạng (VD: "Sảnh tiến", "Lặp đôi", "Tứ quý").</summary>
    public string FormatRhythmName { get; set; } = string.Empty;

    /// <summary>Mô tả chi tiết về cách nhận dạng kiểu định dạng này.</summary>
    public string Description { get; set; } = string.Empty;

    // --- Navigation Property ---
    /// <summary>Danh sách các số SIM có kiểu định dạng số này (quan hệ 1-N).</summary>
    public virtual ICollection<SimNumber> SimNumbers { get; set; } = new List<SimNumber>();
}