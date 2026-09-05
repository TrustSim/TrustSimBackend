using MediatR;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Command yêu cầu đổi mật khẩu tài khoản.
/// </summary>
public record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword,
    Guid UserId = default
) : IRequest<bool>;
