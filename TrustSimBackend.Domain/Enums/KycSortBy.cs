namespace TrustSimBackend.Domain.Enums;

/// <summary>
/// Các tiêu chí sắp xếp cho thẩm định danh tính eKYC (IdentityVerification).
/// </summary>
public enum KycSortBy
{
    SubmittedAt = 1,
    FaceSimilarityScore = 2,
    Status = 3
}
