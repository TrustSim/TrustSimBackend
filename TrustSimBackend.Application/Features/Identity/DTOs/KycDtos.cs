using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Identity.DTOs;

public class IdentityVerificationDto
{
    public Guid Id { get; set; }
    public Guid UserProfileId { get; set; }
    public Guid UserAccountId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string IdentityNumber { get; set; } = string.Empty;
    public VerificationStatus VerificationStatus { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
}

public record SubmitKycRequest(
    string IdentityNumber,
    string? AvatarImg
);

public record ReviewKycRequest(
    VerificationStatus Status,
    string? RejectionReason
);
