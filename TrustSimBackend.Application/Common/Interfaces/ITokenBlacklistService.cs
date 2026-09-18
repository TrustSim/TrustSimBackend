namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ quản lý danh sách đen (Blacklist) thu hồi JWT Access Token và hủy toàn bộ phiên của người dùng.
/// Sử dụng bộ nhớ đệm In-Memory Cache (IMemoryCache) với O(1) Lookup Performance.
/// </summary>
public interface ITokenBlacklistService
{
    /// <summary>
    /// Giải mã chuỗi Access Token (JWT), trích xuất JTI và thời hạn còn lại để đưa vào Blacklist.
    /// </summary>
    /// <param name="rawAccessToken">Chuỗi JWT Access Token</param>
    Task BlacklistAccessTokenAsync(string rawAccessToken);

    /// <summary>
    /// Đưa JTI (JWT ID) của một Access Token cụ thể vào danh sách đen (Blacklist).
    /// Hạn lưu trong Cache bằng đúng thời gian sống còn lại của Token (Remaining TTL).
    /// </summary>
    /// <param name="jti">Mã định danh duy nhất của Access Token (JWT ID claim 'jti')</param>
    /// <param name="remainingLifetime">Thời gian sống còn lại của Token</param>
    Task BlacklistTokenAsync(string jti, TimeSpan remainingLifetime);

    /// <summary>
    /// Kiểm tra JTI của Access Token có nằm trong Blacklist không.
    /// </summary>
    /// <param name="jti">Mã định danh duy nhất của Access Token</param>
    /// <returns>True nếu Token đã bị thu hồi / đăng xuất</returns>
    Task<bool> IsTokenBlacklistedAsync(string jti);

    /// <summary>
    /// Đăng xuất toàn bộ các phiên của một người dùng trên mọi thiết bị.
    /// Lưu mốc thời gian RevokeAllBeforeUtc vào MemoryCache theo UserId.
    /// </summary>
    /// <param name="userId">Mã định danh người dùng</param>
    /// <param name="maxTokenLifetime">Thời gian sống tối đa của Token (TTL của bản ghi Cache)</param>
    Task RevokeAllUserTokensAsync(Guid userId, TimeSpan maxTokenLifetime);

    /// <summary>
    /// Kiểm tra Token được phát hành trước mốc RevokeAllBeforeUtc của người dùng hay không.
    /// </summary>
    /// <param name="userId">Mã định danh người dùng</param>
    /// <param name="tokenIssuedAtUtc">Thời điểm Token được cấp phát (UTC)</param>
    /// <returns>True nếu Token được cấp trước thời điểm Revoke All</returns>
    Task<bool> IsUserRevokedAsync(Guid userId, DateTime tokenIssuedAtUtc);
}
