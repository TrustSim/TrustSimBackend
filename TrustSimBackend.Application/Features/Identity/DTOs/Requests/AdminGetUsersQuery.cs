using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

public record AdminGetUsersQuery(
    AdminUsersQueryFilter Filter
) : IRequest<PagedResult<AdminUserDetailDto>>;
