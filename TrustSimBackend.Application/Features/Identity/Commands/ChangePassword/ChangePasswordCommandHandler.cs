using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands.ChangePassword;

/// <summary>
/// Xử lý nghiệp vụ đổi mật khẩu tài khoản.
/// </summary>
public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, ApiResponse<bool>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public ChangePasswordCommandHandler(
        IUserAccountRepository userAccountRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _userAccountRepository = userAccountRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<bool>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userAccountRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException(nameof(UserAccount), request.UserId);
        }

        // 1. Kiểm tra mật khẩu hiện tại
        var isCurrentPasswordValid = _passwordHasher.VerifyPassword(request.CurrentPassword, user.PassWordHash);
        if (!isCurrentPasswordValid)
        {
            throw new BadRequestException("Mật khẩu hiện tại không chính xác.");
        }

        // 2. Băm mật khẩu mới và cập nhật
        user.PassWordHash = _passwordHasher.HashPassword(request.NewPassword);
        _userAccountRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.SuccessResult(true, "Đổi mật khẩu thành công.");
    }
}
