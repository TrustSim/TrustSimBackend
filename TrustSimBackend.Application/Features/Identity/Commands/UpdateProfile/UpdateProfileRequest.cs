namespace TrustSimBackend.Application.Features.Identity.Commands.UpdateProfile;

/// <summary>
/// DTO Request cập nhật hồ sơ cá nhân nhận từ Client.
/// </summary>
public record UpdateProfileRequest(
    string FullName,
    DateTime? DateOfBirth,
    string Address,
    string IdentityNumber,
    string PhoneNumber
);
