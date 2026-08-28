using MediatR;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Features.Identity.Commands.ChangePassword;

/// <summary>
/// Command đổi mật khẩu tài khoản người dùng.
/// </summary>
public record ChangePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword
) : IRequest<ApiResponse<bool>>;
