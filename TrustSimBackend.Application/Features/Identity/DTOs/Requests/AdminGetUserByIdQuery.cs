using MediatR;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

public record AdminGetUserByIdQuery(Guid Id) : IRequest<AdminUserDetailDto>;
