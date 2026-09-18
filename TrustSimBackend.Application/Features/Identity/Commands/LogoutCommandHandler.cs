using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Xử lý nghiệp vụ đăng xuất:
/// 1. Đưa Access Token hiện tại vào ITokenBlacklistService (MemoryCache).
/// 2. Tìm Refresh Token tương ứng trong DB và thu hồi (IsRevoked = true).
/// 3. Xóa bỏ 2 HttpOnly Cookie (accessToken & refreshToken).
/// 4. Trả về true khi thành công.
/// </summary>
public class LogoutCommandHandler : IRequestHandler<LogoutCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICookieService _cookieService;
    private readonly ITokenBlacklistService _tokenBlacklistService;
    private readonly ILogger<LogoutCommandHandler> _logger;

    public LogoutCommandHandler(
        IUnitOfWork unitOfWork,
        ICookieService cookieService,
        ITokenBlacklistService tokenBlacklistService,
        ILogger<LogoutCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _cookieService = cookieService;
        _tokenBlacklistService = tokenBlacklistService;
        _logger = logger;
    }

    public async Task<bool> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        // 1. Blacklist Access Token hiện tại (Single Session Revocation)
        var accessTokenStr = !string.IsNullOrWhiteSpace(request.AccessToken)
            ? request.AccessToken.Trim()
            : _cookieService.GetAccessToken();

        if (!string.IsNullOrWhiteSpace(accessTokenStr))
        {
            await _tokenBlacklistService.BlacklistAccessTokenAsync(accessTokenStr);
        }

        // 2. Thu hồi Refresh Token trong Database
        var refreshTokenStr = !string.IsNullOrWhiteSpace(request.RefreshToken)
            ? request.RefreshToken.Trim()
            : _cookieService.GetRefreshToken();

        if (!string.IsNullOrWhiteSpace(refreshTokenStr))
        {
            var existingToken = await _unitOfWork.RefreshTokens.GetByTokenAsync(refreshTokenStr, cancellationToken);
            if (existingToken != null && !existingToken.IsRevoked)
            {
                existingToken.IsRevoked = true;
                existingToken.RevokedAt = DateTime.UtcNow;
                existingToken.RevokedByIp = request.IpAddress;
                existingToken.ReasonRevoked = "User Logout";
                _unitOfWork.RefreshTokens.Update(existingToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        // 3. Xóa Cookies xác thực
        _cookieService.ClearAuthCookies();

        return true;
    }
}

