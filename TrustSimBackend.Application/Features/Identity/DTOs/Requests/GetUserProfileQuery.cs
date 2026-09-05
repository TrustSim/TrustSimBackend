using MediatR;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Query lấy thông tin hồ sơ của người dùng theo UserId.
/// </summary>
public record GetUserProfileQuery(
    Guid UserId
) : IRequest<UserProfileDto>;
