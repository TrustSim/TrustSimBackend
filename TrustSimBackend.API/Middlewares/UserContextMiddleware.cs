using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TrustSimBackend.API.Middlewares;

/// <summary>
/// Middleware trích xuất và xác thực JWT token từ HttpOnly Cookie (accessToken) hoặc Header (Authorization).
/// Tự động giải mã danh tính người dùng và gắn vào HttpContext.User và HttpContext.Items để sử dụng xuyên suốt hệ thống.
/// </summary>
public class UserContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserContextMiddleware> _logger;

    public UserContextMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<UserContextMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Trích xuất token: Ưu tiên Header Authorization, fallback sang HttpOnly Cookie 'accessToken'
        var token = ExtractToken(context);

        if (!string.IsNullOrWhiteSpace(token))
        {
            try
            {
                var secretKey = _configuration["JwtSettings:SecretKey"] 
                    ?? "TrustSimSuperSecretKey2026WithSufficientLengthForHMACSHA256!";
                var issuer = _configuration["JwtSettings:Issuer"] ?? "TrustSimBackend";
                var audience = _configuration["JwtSettings:Audience"] ?? "TrustSimClient";

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                // Xác thực token và lấy ClaimsPrincipal
                var principal = tokenHandler.ValidateToken(token, validationParameters, out _);

                // Gắn ClaimsPrincipal vào HttpContext.User để ASP.NET Core [Authorize] nhận diện
                context.User = principal;

                // Trích xuất UserId và các Claims vào HttpContext.Items để truy xuất nhanh O(1)
                var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
                {
                    context.Items["UserId"] = userId;
                }

                var roleClaim = principal.FindFirst(ClaimTypes.Role)?.Value;
                if (!string.IsNullOrEmpty(roleClaim))
                {
                    context.Items["UserRole"] = roleClaim;
                }

                var emailClaim = principal.FindFirst(ClaimTypes.Email)?.Value;
                if (!string.IsNullOrEmpty(emailClaim))
                {
                    context.Items["UserEmail"] = emailClaim;
                }

                var nameClaim = principal.FindFirst(ClaimTypes.Name)?.Value;
                if (!string.IsNullOrEmpty(nameClaim))
                {
                    context.Items["UserName"] = nameClaim;
                }
            }
            catch (Exception ex)
            {
                // Token không hợp lệ hoặc đã hết hạn -> Bỏ qua, pipeline Authentication/Authorization sẽ trả về 401 khi endpoint yêu cầu [Authorize]
                _logger.LogDebug(ex, "[UserContextMiddleware] Xác thực token từ Cookie/Header không thành công: {Message}", ex.Message);
            }
        }

        await _next(context);
    }

    private static string? ExtractToken(HttpContext context)
    {
        // 1. Kiểm tra Header Authorization: Bearer <token>
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader["Bearer ".Length..].Trim();
        }

        // 2. Kiểm tra HttpOnly Cookie 'accessToken'
        if (context.Request.Cookies.TryGetValue("accessToken", out var cookieToken) && !string.IsNullOrWhiteSpace(cookieToken))
        {
            return cookieToken.Trim();
        }

        return null;
    }
}
