using TrustSimBackend.Domain.Base;

namespace TrustSimBackend.Domain.Entities.Trade;

/// <summary>
/// Đại diện cho một tệp hình ảnh riêng lẻ nằm trong một bộ hồ sơ bằng chứng (TransferProof).
/// Trạng thái duyệt của ảnh phụ thuộc hoàn toàn vào TransferProof cha.
/// </summary>
public class TransferProofImage : AuditableEntity
{
    public string ImageUrl { get; set; } = string.Empty;

    public string? FileName { get; set; }

    public string? ContentType { get; set; }

    public long? FileSizeBytes { get; set; }

    public int SortOrder { get; set; }

    public Guid TransferProofId { get; set; }

    public virtual TransferProof? TransferProof { get; set; }
}
