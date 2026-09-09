using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Responses;

/// <summary>
/// DTO trả về kết quả ngay sau khi nộp hồ sơ eKYC.
/// </summary>
public class EkycVerificationResultDto
{
    public Guid VerificationId { get; set; }
    public Guid UserProfileId { get; set; }
    public VerificationStatus VerificationStatus { get; set; }
    public bool IsAutoApproved { get; set; }
    public float FaceSimilarityScore { get; set; }
    public string ExtractedFullName { get; set; } = string.Empty;
    public string ExtractedIdentityNumber { get; set; } = string.Empty;
    public DateTime? ExtractedDateOfBirth { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
}
