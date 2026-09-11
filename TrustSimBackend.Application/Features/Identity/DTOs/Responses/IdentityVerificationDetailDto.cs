using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Responses;

/// <summary>
/// DTO chi tiết hồ sơ xác minh KYC (bao gồm bằng chứng hình ảnh và dữ liệu AI) dành cho Admin thẩm định.
/// </summary>
public class IdentityVerificationDetailDto
{
    public Guid Id { get; set; }
    public Guid UserProfileId { get; set; }
    public Guid UserAccountId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string IdentityNumber { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string Address { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;

    // --- Bằng chứng eKYC ---
    public string FrontIdCardUrl { get; set; } = string.Empty;
    public string BackIdCardUrl { get; set; } = string.Empty;
    public string FaceSelfieUrl { get; set; } = string.Empty;
    public float FaceSimilarityScore { get; set; }
    public string? OcrRawData { get; set; }
    public string VerificationMethod { get; set; } = string.Empty;

    // --- Trạng thái & Audit ---
    public VerificationStatus VerificationStatus { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public Guid? VerifiedByUserId { get; set; }
    public string? VerifiedByUserName { get; set; }
}
