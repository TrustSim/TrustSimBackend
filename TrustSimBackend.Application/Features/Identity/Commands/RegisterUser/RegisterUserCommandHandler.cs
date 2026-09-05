using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.DTOs;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands.RegisterUser;

/// <summary>
/// Xử lý nghiệp vụ đăng ký tài khoản người dùng mới:
/// 1. Kiểm tra tính duy nhất (Username, Email, SĐT, CCCD).
/// 2. Băm mật khẩu an toàn.
/// 3. Khởi tạo UserAccount, UserProfile và Wallet.
/// 4. Sinh JWT Access Token & Refresh Token.
/// 5. Lưu Refresh Token và thiết lập HttpOnly Cookies.
/// 6. Trả về kết quả xác thực.
/// </summary>
public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, ApiResponse<AuthResponseDto>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ICookieService _cookieService;

    public RegisterUserCommandHandler(
        IUserAccountRepository userAccountRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        ICookieService cookieService)
    {
        _userAccountRepository = userAccountRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _cookieService = cookieService;
    }

    public async Task<ApiResponse<AuthResponseDto>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var normalizedUserName = request.UserName.Trim();
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedPhone = request.PhoneNumber.Trim();
        var normalizedIdentity = request.IdentityNumber.Trim();

        // 1. Kiểm tra Username đã tồn tại chưa
        if (await _userAccountRepository.ExistsByUserNameAsync(normalizedUserName, cancellationToken))
        {
            throw new BadRequestException($"Tên đăng nhập '{normalizedUserName}' đã được sử dụng.");
        }

        // 2. Kiểm tra Email đã tồn tại chưa
        if (await _userAccountRepository.ExistsByEmailAsync(normalizedEmail, cancellationToken))
        {
            throw new BadRequestException($"Email '{normalizedEmail}' đã được sử dụng.");
        }

        // 3. Kiểm tra Số điện thoại đã tồn tại chưa
        if (await _userAccountRepository.ExistsByPhoneNumberAsync(normalizedPhone, cancellationToken: cancellationToken))
        {
            throw new BadRequestException($"Số điện thoại '{normalizedPhone}' đã được đăng ký bởi tài khoản khác.");
        }

        // 4. Kiểm tra CCCD đã tồn tại chưa
        if (await _userAccountRepository.ExistsByIdentityNumberAsync(normalizedIdentity, cancellationToken: cancellationToken))
        {
            throw new BadRequestException($"Số CCCD/CMND '{normalizedIdentity}' đã được đăng ký bởi tài khoản khác.");
        }

        // 5. Băm mật khẩu
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        // 6. Tạo UserAccount
        var userAccountId = Guid.NewGuid();
        var userAccount = new UserAccount
        {
            Id = userAccountId,
            UserName = normalizedUserName,
            UserEmail = normalizedEmail,
            PassWordHash = passwordHash,
            Role = UserRole.USER,
            IsActive = true
        };

        // 7. Tạo UserProfile liên kết
        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserAccountId = userAccountId,
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            PhoneNumber = normalizedPhone,
            IdentityNumber = normalizedIdentity,
            DateOfBirth = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Address = string.Empty,
            AvatarImg = string.Empty,
            VerificationStatus = VerificationStatus.PENDING_VERIFICATION
        };
        userAccount.UserProfile = userProfile;

        // 8. Tạo Wallet mặc định liên kết
        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            UserAccountId = userAccountId,
            AvailableBalance = 0,
            LockedBalance = 0,
            WalletStatus = WalletStatus.ACTIVE,
            LockVersion = 0
        };
        userAccount.Wallet = wallet;

        // 9. Lưu UserAccount xuống DB
        await _userAccountRepository.AddAsync(userAccount, cancellationToken);

        // 10. Tạo Access Token và Refresh Token
        var tokenResult = _jwtTokenGenerator.GenerateTokens(userAccount, userProfile);

        // 11. Lưu Refresh Token vào DB
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
        await _refreshTokenRepository.AddAsync(refreshTokenEntity, cancellationToken);

        // 12. Commit Transaction
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 13. Ghi HttpOnly Cookies
        _cookieService.SetAuthCookies(
            tokenResult.AccessToken,
            tokenResult.AccessTokenExpiresAt,
            tokenResult.RefreshToken,
            tokenResult.RefreshTokenExpiresAt
        );

        // 14. Đóng gói kết quả DTO
        var authResponse = new AuthResponseDto
        {
            UserId = userAccount.Id,
            UserName = userAccount.UserName,
            Email = userAccount.UserEmail,
            FullName = userProfile.FullName,
            PhoneNumber = userProfile.PhoneNumber,
            Role = userAccount.Role.ToString(),
            Token = tokenResult.AccessToken,
            ExpiresAt = tokenResult.AccessTokenExpiresAt
        };

        return ApiResponse<AuthResponseDto>.SuccessResult(authResponse, "Đăng ký tài khoản thành công.");
    }
}
