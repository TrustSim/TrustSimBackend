using MediatR;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Command cập nhật thông tin hồ sơ cá nhân.
/// </summary>
public record UpdateProfileCommand(
    string FullName,
    DateTime? DateOfBirth,
    string Address,
    string IdentityNumber,
    string PhoneNumber,
    Guid UserId = default
) : IRequest<UserProfileDto>;
