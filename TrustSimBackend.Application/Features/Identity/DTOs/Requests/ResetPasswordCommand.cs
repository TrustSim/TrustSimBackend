using MediatR;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Command yêu cầu đặt lại mật khẩu bằng mã xác thực.
/// </summary>
public record ResetPasswordCommand(
    string Email,
    string ResetCode,
    string NewPassword,
    string ConfirmNewPassword
) : IRequest<bool>;
