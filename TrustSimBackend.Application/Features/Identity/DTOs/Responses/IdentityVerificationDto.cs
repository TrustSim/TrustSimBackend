using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Responses;

/// <summary>
/// Data Transfer Object hiển thị thông tin hồ sơ xác minh danh tính KYC.
/// </summary>
public class IdentityVerificationDto
{
    public Guid Id { get; set; }
    public Guid UserProfileId { get; set; }
    public Guid UserAccountId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string IdentityNumber { get; set; } = string.Empty;
    public string FrontIdCardUrl { get; set; } = string.Empty;
    public string BackIdCardUrl { get; set; } = string.Empty;
    public string FaceSelfieUrl { get; set; } = string.Empty;
    public float FaceSimilarityScore { get; set; }
    public string VerificationMethod { get; set; } = string.Empty;
    public VerificationStatus VerificationStatus { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? VerifiedByUserName { get; set; }
}

