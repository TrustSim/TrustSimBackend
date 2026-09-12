using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Dispute;

/// <summary>
/// Bằng chứng tranh chấp trong giao dịch
/// </summary>
public class DisputeProof : BaseEntity
{
    
    /// <summary>
    /// Link ảnh/video bằng chứng
    /// </summary>
    public string ImageUrl {get;set;}= string.Empty;

    /// <summary>
    /// Mô tả bằng chứng
    /// </summary>
    public string? Description {get;set;}

    /// <summary>
    /// Khóa ngoại trỏ về phiếu tranh chấp
    /// </summary>
    public Guid DisputeTicketId {get;set;}

    /// <summary>
    /// Quan hệ với Phiếu tranh chấp
    /// </summary>
    public virtual DisputeTicket? DisputeTicket {get;set;}

    
}
