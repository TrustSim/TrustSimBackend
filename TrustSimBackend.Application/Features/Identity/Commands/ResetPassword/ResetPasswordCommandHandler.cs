using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands.ResetPassword;

/// <summary>
/// Xử lý nghiệp vụ đặt lại mật khẩu mới.
/// </summary>
public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, ApiResponse<bool>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public ResetPasswordCommandHandler(
        IUserAccountRepository userAccountRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _userAccountRepository = userAccountRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<bool>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _userAccountRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException(nameof(UserAccount), normalizedEmail);
        }

        // Băm mật khẩu mới và cập nhật
        user.PassWordHash = _passwordHasher.HashPassword(request.NewPassword);
        _userAccountRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.SuccessResult(true, "Đặt lại mật khẩu mới thành công. Bạn có thể đăng nhập ngay bây giờ.");
    }
}
