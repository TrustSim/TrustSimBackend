namespace TrustSimBackend.Application.Features.Identity.Commands.UpdateAvatar;

/// <summary>
/// DTO Request cập nhật ảnh đại diện nhận từ Client.
/// </summary>
public record UpdateAvatarRequest(
    string AvatarUrl
);
