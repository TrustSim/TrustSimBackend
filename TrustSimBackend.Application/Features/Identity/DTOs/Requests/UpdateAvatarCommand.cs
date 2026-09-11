using MediatR;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Command cập nhật avatar người dùng.
/// </summary>
public record UpdateAvatarCommand(
    string AvatarUrl,
    Guid UserId = default
) : IRequest<string>;
