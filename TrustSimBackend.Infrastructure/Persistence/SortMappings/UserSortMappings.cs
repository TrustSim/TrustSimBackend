using System.Linq.Expressions;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Infrastructure.Persistence.SortMappings;

/// <summary>
/// Cấu hình Whitelist Mapping sắp xếp cho Người dùng & Thẩm định eKYC (UserAccount, IdentityVerification).
/// </summary>
public static class UserSortMappings
{
    /// <summary>
    /// Sắp xếp cho danh sách tài khoản thành viên hệ thống (UserAccount).
    /// </summary>
    public static readonly IReadOnlyDictionary<UserAccountSortBy, Expression<Func<UserAccount, object>>> UserAccountMappings =
        new Dictionary<UserAccountSortBy, Expression<Func<UserAccount, object>>>
        {
            [UserAccountSortBy.CreatedDate] = u => u.CreatedDate,
            [UserAccountSortBy.UserName] = u => u.UserName ?? string.Empty,
            [UserAccountSortBy.Email] = u => u.Email ?? string.Empty,
            [UserAccountSortBy.Role] = u => u.Role,
            [UserAccountSortBy.WalletBalance] = u => u.Wallet != null ? u.Wallet.AvailableBalance : 0m,
            [UserAccountSortBy.FullName] = u => u.UserProfile != null ? u.UserProfile.FullName : string.Empty
        };

    /// <summary>
    /// Sắp xếp cho hồ sơ thẩm định eKYC (IdentityVerification).
    /// </summary>
    public static readonly IReadOnlyDictionary<KycSortBy, Expression<Func<IdentityVerification, object>>> KycMappings =
        new Dictionary<KycSortBy, Expression<Func<IdentityVerification, object>>>
        {
            [KycSortBy.SubmittedAt] = v => v.SubmittedAt,
            [KycSortBy.FaceSimilarityScore] = v => v.FaceSimilarityScore,
            [KycSortBy.Status] = v => v.VerificationStatus
        };
}
