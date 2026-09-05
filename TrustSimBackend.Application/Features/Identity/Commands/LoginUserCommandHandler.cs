using MediatR;
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
/// 2. Kiểm tra trạng thái hoạt động của tài khoản.
/// 3. So khớp mật khẩu đã băm.
/// 4. Sinh JWT Access Token & Refresh Token.
/// 5. Lưu Refresh Token xuống DB và thiết lập HttpOnly Cookies.
/// 6. Trả về AuthResponseDto.
/// </summary>
public class LoginUserCommandHandler : IRequestHandler<LoginUserCommand, AuthResponseDto>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ICookieService _cookieService;

    public LoginUserCommandHandler(
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

    public async Task<AuthResponseDto> Handle(LoginUserCommand request, CancellationToken cancellationToken)
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

        // 4. Tạo Access Token và Refresh Token
        var tokenResult = _jwtTokenGenerator.GenerateTokens(user, user.UserProfile);

        // 5. Lưu Refresh Token vào Cơ sở Dữ liệu
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

        await _refreshTokenRepository.AddAsync(refreshTokenEntity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 6. Ghi HttpOnly Cookie cho cả Access Token và Refresh Token
        _cookieService.SetAuthCookies(
            tokenResult.AccessToken,
            tokenResult.AccessTokenExpiresAt,
            tokenResult.RefreshToken,
            tokenResult.RefreshTokenExpiresAt
        );

        // 7. Trả về kết quả DTO
        return new AuthResponseDto
        {
            UserId = user.Id,
            UserName = user.UserName,
            Email = user.UserEmail,
            FullName = user.UserProfile?.FullName ?? string.Empty,
            PhoneNumber = user.UserProfile?.PhoneNumber ?? string.Empty,
            Role = user.Role.ToString(),
            Token = tokenResult.AccessToken,
            ExpiresAt = tokenResult.AccessTokenExpiresAt
        };
    }
}
