using MediatR;
using Microsoft.AspNetCore.Identity;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Domain.Entities.Identity;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Xử lý nghiệp vụ đổi mật khẩu tài khoản qua ASP.NET Core Identity UserManager.
/// </summary>
public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, bool>
{
    private readonly UserManager<UserAccount> _userManager;

    public ChangePasswordCommandHandler(UserManager<UserAccount> userManager)
    {
        _userManager = userManager;
    }

    public async Task<bool> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user == null)
        {
            throw new NotFoundException(nameof(UserAccount), request.UserId);
        }

        // Đổi mật khẩu qua UserManager (tự động kiểm tra mật khẩu cũ và băm mật khẩu mới)
        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new BadRequestException($"Đổi mật khẩu không thành công: {errors}");
        }

        return true;
    }
}
