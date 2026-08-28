using MediatR;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Features.Identity.Commands.UpdateAvatar;

/// <summary>
/// Command cập nhật ảnh đại diện người dùng.
/// </summary>
public record UpdateAvatarCommand(
    Guid UserId,
    string AvatarUrl
) : IRequest<ApiResponse<string>>;
