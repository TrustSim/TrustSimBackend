using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.DTOs;

namespace TrustSimBackend.Application.Features.Identity.Queries.GetUserProfile;

/// <summary>
/// Query lấy thông tin chi tiết hồ sơ cá nhân và ví của người dùng theo UserId.
/// </summary>
public record GetUserProfileQuery(Guid UserId) : IRequest<ApiResponse<UserProfileDto>>;
