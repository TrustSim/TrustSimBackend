using MediatR;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Features.Identity.Commands.ForgotPassword;

/// <summary>
/// Command yêu cầu quên mật khẩu (gửi mã xác thực / reset token qua Email).
/// </summary>
public record ForgotPasswordCommand(
    string Email
) : IRequest<ApiResponse<string>>;
