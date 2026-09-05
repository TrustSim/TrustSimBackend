using MediatR;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Command yêu cầu làm mới Access Token bằng Refresh Token (Rotation cơ chế).
/// </summary>
public record RefreshTokenCommand(
    string? RefreshToken = null,
    string? IpAddress = null
) : IRequest<AuthResponseDto>;
