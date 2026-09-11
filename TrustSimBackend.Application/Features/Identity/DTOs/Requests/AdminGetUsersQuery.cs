using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

public record AdminGetUsersQuery(
    string? Keyword = null,
    UserRole? Role = null,
    bool? IsActive = null,
    int PageIndex = 1,
    int PageSize = 20
) : IRequest<PagedResult<AdminUserDetailDto>>;
