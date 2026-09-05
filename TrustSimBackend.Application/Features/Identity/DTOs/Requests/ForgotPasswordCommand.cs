using MediatR;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Command yêu cầu cấp mã xác thực khi quên mật khẩu.
/// </summary>
public record ForgotPasswordCommand(
    string Email
) : IRequest<string>;
