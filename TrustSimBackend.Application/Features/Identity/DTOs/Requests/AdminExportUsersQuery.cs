using MediatR;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

public record AdminExportUsersQuery(AdminUsersQueryFilter? Filter = null) : IRequest<byte[]>;
