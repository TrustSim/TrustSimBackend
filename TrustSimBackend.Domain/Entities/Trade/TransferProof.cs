using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Trade;

/// <summary>
/// Đại diện cho một bộ hồ sơ bằng chứng chuyển nhượng SIM gắn với một phiên đấu giá (Auction)
/// và tiến trình bàn giao (SimTransfer), chứa nhiều hình ảnh bằng chứng con.
/// </summary>
public class TransferProof : AuditableEntity
{
    public string? Description { get; set; }

    public string? RejectionReason { get; set; }

    public ProofStatus ProofStatus { get; set; } = ProofStatus.PENDING;

    public TransferProofType ProofType { get; set; } = TransferProofType.SELLER_HANDOVER;

    public DateTime? VerifiedAt { get; set; }

    public DateTime? WithdrawnAt { get; set; }

    public string? IdempotencyKey { get; set; }

    public Guid AuctionId { get; set; }

    public virtual Auction? Auction { get; set; }

    public Guid? SimTransferId { get; set; }

    public virtual SimTransfer? SimTransfer { get; set; }

    public Guid SubmittedByUserId { get; set; }

    public virtual UserAccount? SubmittedByUser { get; set; }

    public Guid? VerifiedByAdminId { get; set; }

    public virtual UserAccount? VerifiedByAdmin { get; set; }

    private readonly List<TransferProofImage> _images = new();
    public IReadOnlyCollection<TransferProofImage> Images => _images.AsReadOnly();

    /// <summary>
    /// Thêm một hình ảnh bằng chứng vào bộ hồ sơ (chống trùng lặp theo Id và instance).
    /// </summary>
    public void AddImage(TransferProofImage image)
    {
        if (_images.Any(i => i.Id == image.Id) || _images.Contains(image))
        {
            return;
        }

        _images.Add(image);
    }
}
