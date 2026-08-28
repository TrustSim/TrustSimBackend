using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Domain.Entities.Identity;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Triển khai dịch vụ sinh chuỗi JWT Bearer Token.
/// </summary>
public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTime ExpiresAt) GenerateToken(UserAccount user, UserProfile? profile)
    {
        var secretKey = _configuration["JwtSettings:SecretKey"] 
            ?? "TrustSimSuperSecretKey2026WithSufficientLengthForHMACSHA256!";
        var issuer = _configuration["JwtSettings:Issuer"] ?? "TrustSimBackend";
        var audience = _configuration["JwtSettings:Audience"] ?? "TrustSimClient";
        var expiryHours = double.TryParse(_configuration["JwtSettings:ExpiryHours"], out var hours) ? hours : 24;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.UtcNow.AddHours(expiryHours);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Email, user.UserEmail),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("fullName", profile?.FullName ?? string.Empty),
            new("phoneNumber", profile?.PhoneNumber ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return (tokenHandler.WriteToken(token), expiresAt);
    }
}
