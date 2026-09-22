using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Xử lý nghiệp vụ làm mới Access Token:
/// 1. Trích xuất Refresh Token từ Request Body hoặc HttpOnly Cookie.
/// 2. Kiểm tra tính hợp lệ và thời hạn của token.
/// 3. Phát hiện hành vi tái sử dụng Refresh Token (Reuse Detection) -> Thu hồi toàn bộ token nếu có gian lận.
/// 4. Xoay vòng Refresh Token (Token Rotation): vô hiệu hóa token cũ, sinh cặp Access Token & Refresh Token mới.
/// 5. Cập nhật cơ sở dữ liệu và thiết lập HttpOnly Cookies mới.
/// </summary>
public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponseDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ICookieService _cookieService;

    public RefreshTokenCommandHandler(
        IUnitOfWork unitOfWork,
        IJwtTokenGenerator jwtTokenGenerator,
        ICookieService cookieService)
    {
        _unitOfWork = unitOfWork;
        _jwtTokenGenerator = jwtTokenGenerator;
        _cookieService = cookieService;
    }

    public async Task<AuthResponseDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        // 1. Trích xuất Token từ Request Body (ưu tiên nếu có) hoặc Cookie
        var tokenStr = !string.IsNullOrWhiteSpace(request.RefreshToken) 
            ? request.RefreshToken.Trim() 
            : _cookieService.GetRefreshToken();

        if (string.IsNullOrWhiteSpace(tokenStr))
        {
            throw new UnauthorizedException("Không tìm thấy Refresh Token. Vui lòng đăng nhập lại.");
        }

        // 2. Tìm kiếm Token trong Database
        var existingToken = await _unitOfWork.RefreshTokens.GetByTokenAsync(tokenStr, cancellationToken);
        if (existingToken == null)
        {
            _cookieService.ClearAuthCookies();
            throw new UnauthorizedException("Refresh Token không hợp lệ.");
        }

        // 3. Cơ chế Phát hiện Tái sử dụng (Reuse Detection / Breach Detection)
        if (existingToken.IsUsed)
        {
            // Token đã từng dùng bị gửi lại -> Nguy cơ bị đánh cắp phiên!
            await _unitOfWork.RefreshTokens.RevokeAllUserTokensAsync(
                existingToken.UserAccountId,
                "Phát hiện tái sử dụng Refresh Token đã dùng (Token Reuse Detected)",
                request.IpAddress,
                cancellationToken
            );
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _cookieService.ClearAuthCookies();

            throw new UnauthorizedException("Phát hiện phiên đăng nhập bất thường. Toàn bộ phiên làm việc của bạn đã bị thu hồi, vui lòng đăng nhập lại.");
        }

        // 4. Kiểm tra xem Token có bị thu hồi hoặc hết hạn không
        if (existingToken.IsRevoked || existingToken.IsExpired)
        {
            _cookieService.ClearAuthCookies();
            throw new UnauthorizedException("Refresh Token đã hết hạn hoặc bị thu hồi.");
        }

        // 5. Kiểm tra trạng thái tài khoản người dùng
        var user = existingToken.UserAccount;
        if (user == null || !user.IsActive)
        {
            _cookieService.ClearAuthCookies();
            throw new UnauthorizedException("Tài khoản của bạn đã bị khóa hoặc không tồn tại.");
        }

        // 6. Sinh cặp Token mới (Access Token + Refresh Token mới)
        var newTokens = _jwtTokenGenerator.GenerateTokens(user, user.UserProfile, request.IpAddress);

        // 7. Xoay vòng Token (Token Rotation): Đánh dấu token cũ đã dùng và ghi nhận token thay thế
        existingToken.IsUsed = true;
        existingToken.ReplacedByToken = newTokens.RefreshToken;
        _unitOfWork.RefreshTokens.Update(existingToken);

        // 8. Lưu Refresh Token mới vào Database
        var newRefreshTokenEntity = new Domain.Entities.Identity.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserAccountId = user.Id,
            Token = newTokens.RefreshToken,
            JwtId = newTokens.JwtId,
            ExpiresAt = newTokens.RefreshTokenExpiresAt,
            CreatedByIp = request.IpAddress,
            DeviceInfo = existingToken.DeviceInfo,
            IsUsed = false,
            IsRevoked = false
        };
        await _unitOfWork.RefreshTokens.AddAsync(newRefreshTokenEntity, cancellationToken);

        // Commit DB transaction
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 9. Thiết lập HttpOnly Cookie mới
        _cookieService.SetAuthCookies(
            newTokens.AccessToken,
            newTokens.AccessTokenExpiresAt,
            newTokens.RefreshToken,
            newTokens.RefreshTokenExpiresAt
        );

        // 10. Trả về kết quả DTO
        return new AuthResponseDto
        {
            UserId = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            FullName = user.UserProfile?.FullName ?? string.Empty,
            PhoneNumber = user.UserProfile?.PhoneNumber ?? string.Empty,
            Role = user.Role.ToString(),
            Token = newTokens.AccessToken,
            ExpiresAt = newTokens.AccessTokenExpiresAt
        };
    }
}
