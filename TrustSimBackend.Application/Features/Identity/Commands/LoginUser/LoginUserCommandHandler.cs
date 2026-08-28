using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.DTOs;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands.LoginUser;

/// <summary>
/// Xử lý nghiệp vụ đăng nhập người dùng:
/// 1. Tìm tài khoản theo Username hoặc Email.
/// 2. Kiểm tra trạng thái hoạt động của tài khoản.
/// 3. So khớp mật khẩu đã băm.
/// 4. Sinh JWT Token và trả về thông tin đăng nhập.
/// </summary>
public class LoginUserCommandHandler : IRequestHandler<LoginUserCommand, ApiResponse<AuthResponseDto>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public LoginUserCommandHandler(
        IUserAccountRepository userAccountRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _userAccountRepository = userAccountRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<ApiResponse<AuthResponseDto>> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        var identifier = request.UserNameOrEmail.Trim();

        // 1. Tìm kiếm User theo Username hoặc Email
        var user = await _userAccountRepository.GetByUserNameOrEmailAsync(identifier, cancellationToken);

        if (user == null)
        {
            throw new BadRequestException("Tên đăng nhập/Email hoặc mật khẩu không chính xác.");
        }

        // 2. Kiểm tra trạng thái tài khoản
        if (!user.IsActive)
        {
            throw new BadRequestException("Tài khoản của bạn đã bị vô hiệu hóa hoặc khóa tạm thời.");
        }

        // 3. Xác thực mật khẩu
        var isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PassWordHash);
        if (!isPasswordValid)
        {
            throw new BadRequestException("Tên đăng nhập/Email hoặc mật khẩu không chính xác.");
        }

        // 4. Tạo JWT Token
        var (token, expiresAt) = _jwtTokenGenerator.GenerateToken(user, user.UserProfile);

        // 5. Đóng gói kết quả
        var authResponse = new AuthResponseDto
        {
            UserId = user.Id,
            UserName = user.UserName,
            Email = user.UserEmail,
            FullName = user.UserProfile?.FullName ?? string.Empty,
            PhoneNumber = user.UserProfile?.PhoneNumber ?? string.Empty,
            Role = user.Role.ToString(),
            Token = token,
            ExpiresAt = expiresAt
        };

        return ApiResponse<AuthResponseDto>.SuccessResult(authResponse, "Đăng nhập thành công.");
    }
}
