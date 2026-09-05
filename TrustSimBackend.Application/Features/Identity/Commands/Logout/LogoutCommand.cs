using MediatR;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Features.Identity.Commands.Logout;

/// <summary>
/// Command yêu cầu đăng xuất khỏi hệ thống.
/// </summary>
public record LogoutCommand(
    string? RefreshToken,
    string? IpAddress
) : IRequest<ApiResponse<bool>>;
