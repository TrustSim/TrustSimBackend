using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.DTOs;

namespace TrustSimBackend.Application.Features.Identity.Commands.RefreshToken;

/// <summary>
/// Command yêu cầu làm mới Access Token bằng Refresh Token.
/// </summary>
public record RefreshTokenCommand(
    string? RefreshToken,
    string? IpAddress
) : IRequest<ApiResponse<AuthResponseDto>>;
