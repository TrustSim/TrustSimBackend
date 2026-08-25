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
    public Guid DisputeTicketId { get; private set; }
    
    /// <summary>
    /// Khóa ngoại trỏ về Giao dịch gốc
    /// </summary>
    public Guid TradeTransactionId { get; private set; }
    
    /// <summary>
    /// ID người tạo phiếu kiện
    /// </summary>
    public Guid ComplainantId { get; private set; }
    
    /// <summary>
    /// Lý do tranh chấp
    /// </summary>
    public string Reason { get; private set; } = string.Empty;
    
    /// <summary>
    /// Trạng thái tranh chấp
    /// </summary>
    public DisputeStatus Status { get; private set; } // OPEN, INVESTIGATING, RESOLVED...
    
    /// <summary>
    /// ID Admin xử lý
    /// </summary>
    public Guid? ResolvedByAdminId { get; private set; }
    
    /// <summary>
    /// Ghi chú của Admin
    /// </summary>
    public string? AdminResolutionNotes { get; private set; }

    /// <summary>
    /// Quan hệ với Giao dịch
    /// </summary>
    public virtual TradeTransaction? TradeTransaction { get; private set; }

    /// <summary>
    /// Quan hệ với Người tạo phiếu kiện
    /// </summary>
    public virtual UserAccount? Complainant { get; private set; }

    /// <summary>
    /// Quan hệ với Admin xử lý
    /// </summary>
    public virtual UserAccount? ResolvedByAdmin { get; private set; }
    
    // Một vụ kiện có thể có nhiều bằng chứng (tin nhắn, hình ảnh...)
    private readonly List<DisputeProof> _proofs = new();
    public IReadOnlyCollection<DisputeProof> Proofs => _proofs.AsReadOnly();
    
}