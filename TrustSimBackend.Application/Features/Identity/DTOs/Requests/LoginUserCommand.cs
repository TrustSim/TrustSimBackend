using MediatR;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Command yêu cầu đăng nhập bằng Username/Email và Password.
/// </summary>
public record LoginUserCommand(
    string UserNameOrEmail,
    string Password
) : IRequest<AuthResponseDto>;
