using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands.Logout;

/// <summary>
/// Xử lý nghiệp vụ đăng xuất:
/// 1. Tìm Refresh Token tương ứng trong DB và thu hồi (IsRevoked = true).
/// 2. Xóa bỏ 2 HttpOnly Cookie (accessToken & refreshToken).
/// 3. Trả về kết quả thành công.
/// </summary>
public class LogoutCommandHandler : IRequestHandler<LogoutCommand, ApiResponse<bool>>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICookieService _cookieService;

    public LogoutCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        ICookieService cookieService)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _cookieService = cookieService;
    }

    public async Task<ApiResponse<bool>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var tokenStr = !string.IsNullOrWhiteSpace(request.RefreshToken)
            ? request.RefreshToken.Trim()
            : _cookieService.GetRefreshToken();

        if (!string.IsNullOrWhiteSpace(tokenStr))
        {
            var existingToken = await _refreshTokenRepository.GetByTokenAsync(tokenStr, cancellationToken);
            if (existingToken != null && !existingToken.IsRevoked)
            {
                existingToken.IsRevoked = true;
                existingToken.RevokedAt = DateTime.UtcNow;
                existingToken.RevokedByIp = request.IpAddress;
                existingToken.ReasonRevoked = "User Logout";
                _refreshTokenRepository.Update(existingToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        // Xóa Cookies
        _cookieService.ClearAuthCookies();

        return ApiResponse<bool>.SuccessResult(true, "Đăng xuất thành công.");
    }
}
