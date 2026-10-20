using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.System.DTOs.Responses;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.System.DTOs.Requests;

public record GetMyNotificationsQuery(
    Guid UserId,
    NotificationQueryFilter Filter
) : IRequest<PagedResult<NotificationDto>>;
