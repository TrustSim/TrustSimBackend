using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Domain.Entities.Identity;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Triển khai dịch vụ sinh JWT Access Token (ngắn hạn) và chuỗi Refresh Token ngẫu nhiên bảo mật cao (dài hạn).
/// </summary>
public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public TokenGenerationResult GenerateTokens(UserAccount user, UserProfile? profile, string? ipAddress = null)
    {
        var secretKey = _configuration["JwtSettings:SecretKey"] 
            ?? "TrustSimSuperSecretKey2026WithSufficientLengthForHMACSHA256!";
        var issuer = _configuration["JwtSettings:Issuer"] ?? "TrustSimBackend";
        var audience = _configuration["JwtSettings:Audience"] ?? "TrustSimClient";

        // Access Token: mặc định 15 phút (hoặc đọc từ AccessTokenExpiryMinutes)
        var accessExpiryMinutes = double.TryParse(_configuration["JwtSettings:AccessTokenExpiryMinutes"], out var minutes) 
            ? minutes 
            : 15;

        // Refresh Token: mặc định 7 ngày (hoặc đọc từ RefreshTokenExpiryDays)
        var refreshExpiryDays = double.TryParse(_configuration["JwtSettings:RefreshTokenExpiryDays"], out var days) 
            ? days 
            : 7;

        var jwtId = Guid.NewGuid().ToString();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var accessTokenExpiresAt = DateTime.UtcNow.AddMinutes(accessExpiryMinutes);
        var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(refreshExpiryDays);

        // 1. Sinh Access Token (JWT)
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Email, user.UserEmail),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("fullName", profile?.FullName ?? string.Empty),
            new("phoneNumber", profile?.PhoneNumber ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, jwtId)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = accessTokenExpiresAt,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var securityToken = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(securityToken);

        // 2. Sinh Refresh Token ngẫu nhiên (Opaque Token - 64 bytes Base64)
        var refreshToken = GenerateSecureRefreshToken();

        return new TokenGenerationResult(
            accessToken,
            accessTokenExpiresAt,
            refreshToken,
            refreshTokenExpiresAt,
            jwtId
        );
    }

    private static string GenerateSecureRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
