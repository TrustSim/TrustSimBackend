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
/// 1. Kiểm tra tính duy nhất của Username và Email.
/// 2. Băm mật khẩu (BCrypt).
/// 3. Tạo đồng thời UserAccount + UserProfile + Wallet mặc định.
/// 4. Lưu vào Database qua IUnitOfWork.
/// 5. Tạo và trả về JWT Token.
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

        // 3. Băm mật khẩu
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        // 4. Tạo UserAccount
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

        // 5. Tạo UserProfile liên kết
        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserAccountId = userAccountId,
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            PhoneNumber = request.PhoneNumber.Trim(),
            VerificationStatus = VerificationStatus.PENDING_VERIFICATION
        };
        userAccount.UserProfile = userProfile;

        // 6. Tạo Wallet mặc định liên kết
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

        // 7. Lưu xuống DB
        await _userAccountRepository.AddAsync(userAccount, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 8. Tạo JWT Token
        var (token, expiresAt) = _jwtTokenGenerator.GenerateToken(userAccount, userProfile);

        // 9. Đóng gói kết quả
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
