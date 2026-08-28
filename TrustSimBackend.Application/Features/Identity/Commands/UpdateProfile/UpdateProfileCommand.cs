using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.DTOs;

namespace TrustSimBackend.Application.Features.Identity.Commands.UpdateProfile;

/// <summary>
/// Command cập nhật thông tin hồ sơ người dùng.
/// </summary>
public record UpdateProfileCommand(
    Guid UserId,
    string FullName,
    DateTime? DateOfBirth,
    string Address,
    string IdentityNumber,
    string PhoneNumber
) : IRequest<ApiResponse<UserProfileDto>>;
