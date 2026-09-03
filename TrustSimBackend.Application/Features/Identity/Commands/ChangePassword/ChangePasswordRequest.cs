namespace TrustSimBackend.Application.Features.Identity.Commands.ChangePassword;

/// <summary>
/// DTO Request đổi mật khẩu nhận từ Client.
/// </summary>
public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword
);
