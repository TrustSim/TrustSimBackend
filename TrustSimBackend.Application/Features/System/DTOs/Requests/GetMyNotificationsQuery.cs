using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.System.DTOs.Responses;

namespace TrustSimBackend.Application.Features.System.DTOs.Requests;

public record GetMyNotificationsQuery(
    Guid UserId = default,
    bool? IsRead = null,
    int PageIndex = 1,
    int PageSize = 20
) : IRequest<PagedResult<NotificationDto>>;
