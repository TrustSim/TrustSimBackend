using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Xử lý nghiệp vụ xác thực Email OTP và hoàn tất đăng ký tài khoản (Bước 2):
/// 1. Xác thực mã OTP email do người dùng nhập vào qua OtpService.
/// 2. Lấy dữ liệu đăng ký tạm thời (UserName, Email, Password) từ MemoryCache.
/// 3. Khởi tạo UserAccount cùng UserProfile & Wallet liên kết qua ASP.NET Core Identity.
/// 4. Gán quyền mặc định USER.
/// 5. Xóa dữ liệu tạm trong Cache.
/// 6. Sinh JWT Access Token & Refresh Token, lưu Refresh Token vào DB và thiết lập HttpOnly Cookies.
/// 7. Trả về thông tin AuthResponseDto (Đăng nhập tự động ngay sau khi đăng ký thành công).
/// </summary>
public class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand, AuthResponseDto>
{
    private readonly UserManager<UserAccount> _userManager;
    private readonly IOtpService _otpService;
    private readonly IMemoryCache _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ICookieService _cookieService;

    public VerifyEmailCommandHandler(
        UserManager<UserAccount> userManager,
        IOtpService otpService,
        IMemoryCache cache,
        IUnitOfWork unitOfWork,
        IJwtTokenGenerator jwtTokenGenerator,
        ICookieService cookieService)
    {
        _userManager = userManager;
        _otpService = otpService;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _jwtTokenGenerator = jwtTokenGenerator;
        _cookieService = cookieService;
    }

    public async Task<AuthResponseDto> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var cacheKey = $"registration:pending:{normalizedEmail}";

        // 1. Xác thực mã OTP email (chỉ consume khi tạo tài khoản thành công)
        var otpResult = await _otpService.VerifyEmailOtpAsync(
            normalizedEmail,
            request.OtpCode.Trim(),
            OtpPurpose.Registration.ToString(),
            consumeOnSuccess: false);

        if (!otpResult.IsValid)
        {
            throw new BadRequestException(otpResult.Message ?? "Mã xác thực OTP không hợp lệ hoặc đã hết hạn.");
        }

        // 2. Lấy thông tin đăng ký tạm từ MemoryCache
        if (!_cache.TryGetValue<PendingRegistrationModel>(cacheKey, out var pendingData) || pendingData == null)
        {
            throw new BadRequestException("Thông tin đăng ký đã hết hạn hoặc không tồn tại. Vui lòng thực hiện đăng ký lại.");
        }

        // 3. Kiểm tra lại tính duy nhất của Username & Email trong Database
        var existingByUserName = await _userManager.FindByNameAsync(pendingData.UserName);
        if (existingByUserName != null)
        {
            _cache.Remove(cacheKey);
            throw new BadRequestException($"Tên đăng nhập '{pendingData.UserName}' đã được sử dụng.");
        }

        var existingByEmail = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existingByEmail != null)
        {
            _cache.Remove(cacheKey);
            throw new BadRequestException($"Email '{normalizedEmail}' đã được sử dụng.");
        }

        // 4. Khởi tạo UserProfile & Wallet gắn liền với UserAccount
        var userAccountId = Guid.NewGuid();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserAccountId = userAccountId,
            FullName = string.Empty,
            Email = normalizedEmail,
            PhoneNumber = string.Empty,
            IdentityNumber = string.Empty,
            DateOfBirth = null,
            Address = string.Empty,
            AvatarImg = string.Empty,
            VerificationStatus = VerificationStatus.PENDING_VERIFICATION,
            IsActive = true
        };

        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            UserAccountId = userAccountId,
            AvailableBalance = 0,
            LockedBalance = 0,
            WalletStatus = WalletStatus.ACTIVE,
            LockVersion = 0,
            IsActive = true
        };

        var userAccount = new UserAccount
        {
            Id = userAccountId,
            UserName = pendingData.UserName,
            Email = normalizedEmail,
            EmailConfirmed = true,
            Role = UserRole.USER,
            IsActive = true,
            UserProfile = userProfile,
            Wallet = wallet
        };

        var createResult = await _userManager.CreateAsync(userAccount, pendingData.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            throw new BadRequestException($"Đăng ký tài khoản không thành công: {errors}");
        }

        // 5. Gán vai trò USER mặc định
        await _userManager.AddToRoleAsync(userAccount, UserRole.USER.ToString());

        // 6. Tạo Access Token và Refresh Token
        var tokenResult = _jwtTokenGenerator.GenerateTokens(userAccount, userProfile);

        // 7. Lưu Refresh Token vào DB
        var refreshTokenEntity = new Domain.Entities.Identity.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserAccountId = userAccountId,
            Token = tokenResult.RefreshToken,
            JwtId = tokenResult.JwtId,
            ExpiresAt = tokenResult.RefreshTokenExpiresAt,
            IsUsed = false,
            IsRevoked = false
        };
        await _unitOfWork.RefreshTokens.AddAsync(refreshTokenEntity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 8. Hủy mã OTP và thông tin đăng ký tạm khỏi Cache sau khi tạo tài khoản thành công
        await _otpService.VerifyEmailOtpAsync(normalizedEmail, request.OtpCode.Trim(), OtpPurpose.Registration.ToString(), consumeOnSuccess: true);
        _cache.Remove(cacheKey);

        // 9. Ghi HttpOnly Cookies
        _cookieService.SetAuthCookies(
            tokenResult.AccessToken,
            tokenResult.AccessTokenExpiresAt,
            tokenResult.RefreshToken,
            tokenResult.RefreshTokenExpiresAt
        );

        // 10. Trả về kết quả DTO
        return new AuthResponseDto
        {
            UserId = userAccount.Id,
            UserName = userAccount.UserName,
            Email = userAccount.Email ?? string.Empty,
            FullName = userProfile.FullName,
            PhoneNumber = userProfile.PhoneNumber,
            Role = userAccount.Role.ToString(),
            Token = tokenResult.AccessToken,
            ExpiresAt = tokenResult.AccessTokenExpiresAt
        };
    }
}
