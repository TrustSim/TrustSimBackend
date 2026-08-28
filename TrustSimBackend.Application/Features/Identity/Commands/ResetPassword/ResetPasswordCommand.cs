using MediatR;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Features.Identity.Commands.ResetPassword;

/// <summary>
/// Command đặt lại mật khẩu mới bằng mã xác thực.
/// </summary>
public record ResetPasswordCommand(
    string Email,
    string ResetCode,
    string NewPassword,
    string ConfirmNewPassword
) : IRequest<ApiResponse<bool>>;
