using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Identity;

/// <summary>
/// Ghi lại lịch sử mỗi lần người dùng gửi yêu cầu xác minh danh tính eKYC (CMND/CCCD/Hộ chiếu).
/// Lưu trữ toàn bộ bằng chứng hình ảnh, kết quả OCR và điểm số tương đồng khuôn mặt từ AWS Rekognition.
/// Không được phép xóa — là bằng chứng pháp lý về quá trình định danh người dùng.
/// </summary>
public class IdentityVerification : AuditableEntity
{

    // --- Foreign Key ---
    /// <summary>ID của hồ sơ cá nhân (<see cref="UserProfile"/>) thực hiện yêu cầu xác minh này.</summary>
    public Guid UserProfileId { get; set; }

    /// <summary>ID của Admin phê duyệt (nếu xử lý thủ công). Null nếu duyệt tự động bởi hệ thống AI.</summary>
    public Guid? VerifiedByUserId { get; set; }

    // --- Bằng chứng Hình ảnh & Dữ liệu eKYC ---
    /// <summary>Đường dẫn ảnh mặt trước CMND/CCCD.</summary>
    public string FrontIdCardUrl { get; set; } = string.Empty;

    /// <summary>Đường dẫn ảnh mặt sau CMND/CCCD.</summary>
    public string BackIdCardUrl { get; set; } = string.Empty;

    /// <summary>Đường dẫn ảnh chụp chân dung / selfie trực tiếp.</summary>
    public string FaceSelfieUrl { get; set; } = string.Empty;

    /// <summary>Tỷ lệ so khớp khuôn mặt giữa ảnh chân dung thẻ và ảnh selfie (từ 0.0 đến 100.0%).</summary>
    public float FaceSimilarityScore { get; set; }

    /// <summary>Dữ liệu JSON thô nhận dạng được từ AWS Rekognition OCR.</summary>
    public string? OcrRawData { get; set; }

    /// <summary>Phương thức xác minh: AUTOMATED_AWS_REKOGNITION hoặc MANUAL_ADMIN_REVIEW.</summary>
    public string VerificationMethod { get; set; } = "AUTOMATED_AWS_REKOGNITION";

    // --- Trạng thái ---
    /// <summary>Trạng thái xử lý của yêu cầu xác minh: PENDING_VERIFICATION, VERIFIED, REJECTED, EXPIRED.</summary>
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.PENDING_VERIFICATION;

    /// <summary>Thời điểm người dùng nộp hồ sơ xác minh lên hệ thống.</summary>
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Thời điểm Admin duyệt hoặc từ chối hồ sơ. Null nếu chưa được xử lý.</summary>
    public DateTime? VerifiedAt { get; set; }

    /// <summary>Lý do từ chối do Admin hoặc hệ thống điền vào khi trạng thái là REJECTED. Rỗng nếu được duyệt.</summary>
    public string? RejectionReason { get; set; } = string.Empty;

    // --- Navigation Property ---
    /// <summary>Hồ sơ cá nhân đã gửi yêu cầu xác minh này.</summary>
    public virtual UserProfile? UserProfile { get; set; }

    /// <summary>Tài khoản Admin đã thực hiện phê duyệt/từ chối hồ sơ (nếu có).</summary>
    public virtual UserAccount? VerifiedByUser { get; set; }
}