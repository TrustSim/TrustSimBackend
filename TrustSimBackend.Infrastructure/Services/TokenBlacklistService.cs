using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Interfaces;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Triển khai dịch vụ ITokenBlacklistService quản lý Blacklist JWT Access Token và Revoke All Sessions.
/// Sử dụng IMemoryCache của ASP.NET Core để kiểm tra O(1) hiệu năng cao, thread-safe.
/// </summary>
public class TokenBlacklistService : ITokenBlacklistService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<TokenBlacklistService> _logger;

    private const string TokenBlacklistPrefix = "blacklist_token_";
    private const string UserRevokePrefix = "user_revoke_";

    public TokenBlacklistService(
        IMemoryCache cache,
        ILogger<TokenBlacklistService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Task BlacklistAccessTokenAsync(string rawAccessToken)
    {
        if (string.IsNullOrWhiteSpace(rawAccessToken))
        {
            return Task.CompletedTask;
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            if (tokenHandler.CanReadToken(rawAccessToken))
            {
                var jwtToken = tokenHandler.ReadJwtToken(rawAccessToken);
                var jti = jwtToken.Id;
                var expiresAt = jwtToken.ValidTo;
                var remainingLifetime = expiresAt - DateTime.UtcNow;

                if (!string.IsNullOrEmpty(jti) && remainingLifetime > TimeSpan.Zero)
                {
                    return BlacklistTokenAsync(jti, remainingLifetime);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[TokenBlacklistService] Không thể giải mã Access Token để Blacklist: {Message}", ex.Message);
        }

        return Task.CompletedTask;
    }

    public Task BlacklistTokenAsync(string jti, TimeSpan remainingLifetime)
    {
        if (string.IsNullOrWhiteSpace(jti) || remainingLifetime <= TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        var key = $"{TokenBlacklistPrefix}{jti}";
        _cache.Set(key, true, remainingLifetime);
        _logger.LogInformation("[TokenBlacklistService] Đã đưa JTI {Jti} vào Blacklist với TTL = {Lifetime}", jti, remainingLifetime);

        return Task.CompletedTask;
    }

    public Task<bool> IsTokenBlacklistedAsync(string jti)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            return Task.FromResult(false);
        }

        var key = $"{TokenBlacklistPrefix}{jti}";
        var isBlacklisted = _cache.TryGetValue(key, out _);
        return Task.FromResult(isBlacklisted);
    }

    public Task RevokeAllUserTokensAsync(Guid userId, TimeSpan maxTokenLifetime)
    {
        if (userId == Guid.Empty)
        {
            return Task.CompletedTask;
        }

        var key = $"{UserRevokePrefix}{userId}";
        var now = DateTime.UtcNow;
        _cache.Set(key, now, maxTokenLifetime);
        _logger.LogInformation("[TokenBlacklistService] Đã thu hồi toàn bộ phiên của UserId {UserId} tại mốc {Timestamp}", userId, now);

        return Task.CompletedTask;
    }

    public Task<bool> IsUserRevokedAsync(Guid userId, DateTime tokenIssuedAtUtc)
    {
        if (userId == Guid.Empty)
        {
            return Task.FromResult(false);
        }

        var key = $"{UserRevokePrefix}{userId}";
        if (_cache.TryGetValue(key, out DateTime revokedBeforeUtc))
        {
            // Nếu token được cấp trước hoặc tại thời điểm Revoke All -> Token bị từ chối
            return Task.FromResult(tokenIssuedAtUtc <= revokedBeforeUtc);
        }

        return Task.FromResult(false);
    }
}
