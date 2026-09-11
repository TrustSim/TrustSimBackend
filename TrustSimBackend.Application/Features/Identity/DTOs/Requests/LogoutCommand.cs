using MediatR;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Command yêu cầu đăng xuất và thu hồi Refresh Token.
/// </summary>
public record LogoutCommand(
    string? RefreshToken = null,
    string? IpAddress = null
) : IRequest<bool>;
