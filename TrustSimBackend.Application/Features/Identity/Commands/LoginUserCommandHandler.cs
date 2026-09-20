using MediatR;
using Microsoft.AspNetCore.Identity;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Xử lý nghiệp vụ đăng nhập người dùng:
/// 1. Tìm tài khoản theo Username hoặc Email.
/// 2. Kiểm tra trạng thái hoạt động và chính sách khóa tài khoản (Lockout) của ASP.NET Identity.
/// 3. So khớp mật khẩu qua UserManager.
/// 4. Sinh JWT Access Token & Refresh Token.
/// 5. Lưu Refresh Token xuống DB và thiết lập HttpOnly Cookies.
/// 6. Trả về AuthResponseDto.
/// </summary>
public class LoginUserCommandHandler : IRequestHandler<LoginUserCommand, AuthResponseDto>
{
    private readonly UserManager<UserAccount> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ICookieService _cookieService;

    public LoginUserCommandHandler(
        UserManager<UserAccount> userManager,
        IUnitOfWork unitOfWork,
        IJwtTokenGenerator jwtTokenGenerator,
        ICookieService cookieService)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _jwtTokenGenerator = jwtTokenGenerator;
        _cookieService = cookieService;
    }

    public async Task<AuthResponseDto> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        var identifier = request.UserNameOrEmail.Trim();

        // 1. Tìm kiếm User theo Username hoặc Email (kèm UserProfile, Wallet)
        var user = await _unitOfWork.Users.GetByUserNameOrEmailAsync(identifier, cancellationToken);

        if (user == null)
        {
            throw new BadRequestException("Tên đăng nhập/Email hoặc mật khẩu không chính xác.");
        }

        // 2. Kiểm tra tài khoản có đang bị khóa (Lockout do nhập sai mật khẩu nhiều lần)
        if (await _userManager.IsLockedOutAsync(user))
        {
            var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
            var remainingMinutes = lockoutEnd.HasValue
                ? Math.Max(1, (int)Math.Ceiling((lockoutEnd.Value - DateTimeOffset.UtcNow).TotalMinutes))
                : 15;
            throw new BadRequestException($"Tài khoản của bạn đã bị tạm khóa do nhập sai mật khẩu nhiều lần. Vui lòng thử lại sau {remainingMinutes} phút.");
        }

        // 3. Kiểm tra trạng thái hoạt động tài khoản
        if (!user.IsActive || user.IsDeleted)
        {
            throw new BadRequestException("Tài khoản của bạn đã bị vô hiệu hóa hoặc khóa tạm thời.");
        }

        // 4. Xác thực mật khẩu qua UserManager
        var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isPasswordValid)
        {
            await _userManager.AccessFailedAsync(user);
            throw new BadRequestException("Tên đăng nhập/Email hoặc mật khẩu không chính xác.");
        }

        // Reset số lần đăng nhập sai khi đăng nhập thành công
        await _userManager.ResetAccessFailedCountAsync(user);

        // 5. Tạo Access Token và Refresh Token
        var tokenResult = _jwtTokenGenerator.GenerateTokens(user, user.UserProfile);

        // 6. Lưu Refresh Token vào Cơ sở Dữ liệu
        var refreshTokenEntity = new Domain.Entities.Identity.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserAccountId = user.Id,
            Token = tokenResult.RefreshToken,
            JwtId = tokenResult.JwtId,
            ExpiresAt = tokenResult.RefreshTokenExpiresAt,
            IsUsed = false,
            IsRevoked = false
        };

        await _unitOfWork.RefreshTokens.AddAsync(refreshTokenEntity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 7. Ghi HttpOnly Cookie cho cả Access Token và Refresh Token
        _cookieService.SetAuthCookies(
            tokenResult.AccessToken,
            tokenResult.AccessTokenExpiresAt,
            tokenResult.RefreshToken,
            tokenResult.RefreshTokenExpiresAt
        );

        // 8. Trả về kết quả DTO
        return new AuthResponseDto
        {
            UserId = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            FullName = user.UserProfile?.FullName ?? string.Empty,
            PhoneNumber = user.UserProfile?.PhoneNumber ?? string.Empty,
            Role = user.Role.ToString(),
            Token = tokenResult.AccessToken,
            ExpiresAt = tokenResult.AccessTokenExpiresAt
        };
    }
}
