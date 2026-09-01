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
/// Xử lý nghiệp vụ đăng ký tài khoản người dùng mới.
/// </summary>
public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, ApiResponse<AuthResponseDto>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public RegisterUserCommandHandler(
        IUserAccountRepository userAccountRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _userAccountRepository = userAccountRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
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
            DateOfBirth = request.DateOfBirth ?? new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Address = request.Address?.Trim() ?? string.Empty,
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

        // 9. Lưu xuống DB
        await _userAccountRepository.AddAsync(userAccount, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 10. Tạo JWT Token
        var (token, expiresAt) = _jwtTokenGenerator.GenerateToken(userAccount, userProfile);

        // 11. Đóng gói kết quả
        var authResponse = new AuthResponseDto
        {
            UserId = userAccount.Id,
            UserName = userAccount.UserName,
            Email = userAccount.UserEmail,
            FullName = userProfile.FullName,
            PhoneNumber = userProfile.PhoneNumber,
            Role = userAccount.Role.ToString(),
            Token = token,
            ExpiresAt = expiresAt
        };

        return ApiResponse<AuthResponseDto>.SuccessResult(authResponse, "Đăng ký tài khoản thành công.");
    }
}
