using MediatR;

namespace TrustSimBackend.Application.Features.System.DTOs.Requests;

public record GetUnreadNotificationCountQuery(Guid UserId) : IRequest<int>;
