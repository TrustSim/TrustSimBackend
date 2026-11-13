using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Sim;

/// <summary>
/// Ghi lại lịch sử mỗi lần người dùng gửi yêu cầu xác minh quyền sở hữu SIM (ví dụ: thực hiện cuộc gọi kiểm tra).
/// Mỗi lần nộp bằng chứng là một bản ghi mới trong <see cref="SimOwnership"/>.
/// Không bao giờ bị xóa — là bằng chứng pháp lý về quá trình xác minh chính chủ.
/// </summary>
public class SimVerification : AuditableEntity
{
    /// <summary>Trạng thái xử lý của yêu cầu xác minh: PENDING_VERIFICATION, VERIFIED, REJECTED, EXPIRED.</summary>
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.PENDING_VERIFICATION;

    /// <summary>Phương thức xác minh (ví dụ: OTP_CALL, TELCO_GATEWAY, AUCTION_TRANSFER_APPROVED).</summary>
    public string? VerificationMethod { get; set; }

    /// <summary>Thời điểm người dùng gửi yêu cầu xác minh.</summary>
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Thời điểm yêu cầu được duyệt hoặc từ chối. Null nếu chưa xử lý.</summary>
    public DateTime? VerifiedAt { get; set; }

    /// <summary>Lý do từ chối (nếu Admin/hệ thống từ chối yêu cầu). Null nếu được duyệt.</summary>
    public string? RejectionReason { get; set; }

    // --- Foreign Key ---
    /// <summary>ID của bản ghi quyền sở hữu SIM (<see cref="SimOwnership"/>) đang được xác minh.</summary>
    public Guid SimOwnershipId { get; set; }

    // --- Navigation Property ---
    /// <summary>Bản ghi quyền sở hữu SIM cần xác minh.</summary>
    public virtual SimOwnership? SimOwnership { get; set; }
}
