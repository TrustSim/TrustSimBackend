using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.DTOs;

namespace TrustSimBackend.Application.Features.Identity.Commands.LoginUser;

/// <summary>
/// Command yêu cầu đăng nhập vào hệ thống.
/// </summary>
public record LoginUserCommand(
    string UserNameOrEmail,
    string Password
) : IRequest<ApiResponse<AuthResponseDto>>;
