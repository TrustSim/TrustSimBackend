using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Xử lý nghiệp vụ đăng xuất và thu hồi toàn bộ các phiên hoạt động của người dùng:
/// 1. Thu hồi toàn bộ Refresh Tokens còn hiệu lực trong Database.
/// 2. Lưu mốc thời gian RevokeAllBeforeUtc vào MemoryCache qua ITokenBlacklistService.
/// 3. Xóa Cookies xác thực trên máy gọi lệnh.
/// </summary>
public class RevokeAllSessionsCommandHandler : IRequestHandler<RevokeAllSessionsCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenBlacklistService _tokenBlacklistService;
    private readonly ICookieService _cookieService;
    private readonly ILogger<RevokeAllSessionsCommandHandler> _logger;

    public RevokeAllSessionsCommandHandler(
        IUnitOfWork unitOfWork,
        ITokenBlacklistService tokenBlacklistService,
        ICookieService cookieService,
        ILogger<RevokeAllSessionsCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenBlacklistService = tokenBlacklistService;
        _cookieService = cookieService;
        _logger = logger;
    }

    public async Task<bool> Handle(RevokeAllSessionsCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            return false;
        }

        // 1. Thu hồi toàn bộ Refresh Token của người dùng trong DB
        await _unitOfWork.RefreshTokens.RevokeAllUserTokensAsync(
            request.UserId, 
            "Revoke All Sessions", 
            request.IpAddress, 
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 2. Ghi nhận mốc RevokeAllBeforeUtc vào ITokenBlacklistService (TTL = 7 ngày)
        await _tokenBlacklistService.RevokeAllUserTokensAsync(request.UserId, TimeSpan.FromDays(7));

        // 3. Xóa Cookies trên máy hiện tại
        _cookieService.ClearAuthCookies();

        _logger.LogInformation("[RevokeAllSessions] Đã thu hồi toàn bộ phiên đăng nhập của UserId {UserId}", request.UserId);
        return true;
    }
}
