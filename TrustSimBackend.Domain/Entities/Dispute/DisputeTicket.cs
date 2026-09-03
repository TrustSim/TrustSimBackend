using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Dispute;

/// <summary>
/// Phiếu tranh chấp trong giao dịch
/// </summary>
public class DisputeTicket : AuditableEntity
{
    /// <summary>
    /// Khóa chính
    /// </summary>
    public Guid DisputeTicketId { get => Id; private set => Id = value; }
    
    /// <summary>
    /// Khóa ngoại trỏ về Giao dịch gốc
    /// </summary>
    public Guid TradeTransactionId { get; set; }
    
    /// <summary>
    /// ID người tạo phiếu kiện
    /// </summary>
    public Guid ComplainantId { get; set; }
    
    /// <summary>
    /// Lý do tranh chấp
    /// </summary>
    public string Reason { get; set; } = string.Empty;
    
    /// <summary>
    /// Trạng thái tranh chấp
    /// </summary>
    public DisputeStatus Status { get; set; } = DisputeStatus.OPEN;
    
    /// <summary>
    /// ID Admin xử lý
    /// </summary>
    public Guid? ResolvedByAdminId { get; set; }
    
    /// <summary>
    /// Ghi chú của Admin
    /// </summary>
    public string? AdminResolutionNotes { get; set; }

    /// <summary>
    /// Quan hệ với Giao dịch
    /// </summary>
    public virtual TradeTransaction? TradeTransaction { get; set; }

    /// <summary>
    /// Quan hệ với Người tạo phiếu kiện
    /// </summary>
    public virtual UserAccount? Complainant { get; set; }

    /// <summary>
    /// Quan hệ với Admin xử lý
    /// </summary>
    public virtual UserAccount? ResolvedByAdmin { get; set; }
    
    /// <summary>
    /// Danh sách bằng chứng
    /// </summary>
    public virtual ICollection<DisputeProof> Proofs { get; set; } = new List<DisputeProof>();
}