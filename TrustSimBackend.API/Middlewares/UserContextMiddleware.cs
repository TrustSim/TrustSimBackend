using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.API.Middlewares;

/// <summary>
/// Middleware trích xuất, xác thực JWT token từ HttpOnly Cookie (accessToken) hoặc Header (Authorization).
/// Tích hợp:
/// 1. Kiểm tra Token Blacklist (O(1) MemoryCache) cho từng phiên/thiết bị khi đăng xuất.
/// 2. Kiểm tra Revoke All Sessions theo UserId.
/// 3. Kiểm tra tự động Endpoint Metadata [Authorize] và [Authorize(Roles = "...")] trước khi vào Controller.
/// 4. Chuẩn hóa format phản hồi HTTP 401 Unauthorized / 403 Forbidden theo ApiResponse chuẩn.
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

    public async Task InvokeAsync(HttpContext context, ITokenBlacklistService tokenBlacklistService)
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
                    ClockSkew = TimeSpan.Zero,
                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = ClaimTypes.Name
                };

                // Xác thực token và lấy ClaimsPrincipal
                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

                if (validatedToken is JwtSecurityToken jwtToken)
                {
                    var jti = jwtToken.Id;
                    var issuedAt = jwtToken.ValidFrom;

                    // 1.1. Kiểm tra Token Blacklist (Từng phiên / thiết bị)
                    if (!string.IsNullOrEmpty(jti) && await tokenBlacklistService.IsTokenBlacklistedAsync(jti))
                    {
                        _logger.LogWarning("[UserContextMiddleware] Yêu cầu bị từ chối: Token JTI {Jti} nằm trong Blacklist (đã đăng xuất).", jti);
                        await WriteErrorResponseAsync(context, StatusCodes.Status401Unauthorized, "Phiên đăng nhập này đã đăng xuất. Vui lòng đăng nhập lại.");
                        return;
                    }

                    // 1.2. Trích xuất UserId
                    var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? principal.FindFirst("nameid")?.Value
                        ?? principal.FindFirst("sub")?.Value;

                    if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
                    {
                        // 1.3. Kiểm tra Revoke All Sessions theo UserId
                        if (await tokenBlacklistService.IsUserRevokedAsync(userId, issuedAt))
                        {
                            _logger.LogWarning("[UserContextMiddleware] Yêu cầu bị từ chối: UserId {UserId} đã thu hồi toàn bộ phiên trước đó.", userId);
                            await WriteErrorResponseAsync(context, StatusCodes.Status401Unauthorized, "Tài khoản đã đăng xuất khỏi tất cả các thiết bị. Vui lòng đăng nhập lại.");
                            return;
                        }

                        context.Items["UserId"] = userId;
                    }

                    // Trích xuất Role và chuẩn hóa vào ClaimsIdentity
                    var roleClaim = principal.FindFirst(ClaimTypes.Role)?.Value 
                        ?? principal.FindFirst("role")?.Value;

                    if (!string.IsNullOrEmpty(roleClaim))
                    {
                        context.Items["UserRole"] = roleClaim;
                        if (principal.Identity is ClaimsIdentity identity)
                        {
                            if (!identity.HasClaim(ClaimTypes.Role, roleClaim))
                            {
                                identity.AddClaim(new Claim(ClaimTypes.Role, roleClaim));
                            }
                            if (!identity.HasClaim("role", roleClaim))
                            {
                                identity.AddClaim(new Claim("role", roleClaim));
                            }
                        }
                    }

                    var emailClaim = principal.FindFirst(ClaimTypes.Email)?.Value
                        ?? principal.FindFirst("email")?.Value;

                    if (!string.IsNullOrEmpty(emailClaim))
                    {
                        context.Items["UserEmail"] = emailClaim;
                    }

                    var nameClaim = principal.FindFirst(ClaimTypes.Name)?.Value
                        ?? principal.FindFirst("unique_name")?.Value;

                    if (!string.IsNullOrEmpty(nameClaim))
                    {
                        context.Items["UserName"] = nameClaim;
                    }

                    // Gán ClaimsPrincipal vào HttpContext.User
                    context.User = principal;
                }
            }
            catch (Exception ex)
            {
                // Token không hợp lệ hoặc đã hết hạn
                _logger.LogDebug(ex, "[UserContextMiddleware] Xác thực token từ Cookie/Header không thành công: {Message}", ex.Message);
            }
        }

        // 2. Kiểm tra Authentication & Authorization dựa trên Endpoint Metadata trước khi vào Controller
        var endpoint = context.GetEndpoint();
        if (endpoint != null)
        {
            var allowAnonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() != null;
            var authorizeData = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>() ?? Array.Empty<IAuthorizeData>();

            if (!allowAnonymous && authorizeData.Any())
            {
                // 2.1. Yêu cầu Authenticated
                if (context.User?.Identity?.IsAuthenticated != true)
                {
                    await WriteErrorResponseAsync(context, StatusCodes.Status401Unauthorized, "Yêu cầu xác thực tài khoản. Vui lòng đăng nhập để tiếp tục.");
                    return;
                }

                // 2.2. Kiểm tra Roles
                var requiredRoles = authorizeData
                    .Where(a => !string.IsNullOrWhiteSpace(a.Roles))
                    .SelectMany(a => a.Roles!.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    .ToList();

                if (requiredRoles.Any())
                {
                    var userRole = context.Items["UserRole"]?.ToString() ?? string.Empty;
                    var hasRole = requiredRoles.Any(role => 
                        context.User.IsInRole(role) || 
                        string.Equals(userRole, role, StringComparison.OrdinalIgnoreCase));

                    if (!hasRole)
                    {
                        await WriteErrorResponseAsync(context, StatusCodes.Status403Forbidden, "Bạn không có quyền truy cập chức năng này.");
                        return;
                    }
                }
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

    private static async Task WriteErrorResponseAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var response = ApiResponse<object>.FailureResult(message);
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}

