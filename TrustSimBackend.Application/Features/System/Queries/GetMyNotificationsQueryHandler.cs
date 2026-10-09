using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.System.DTOs.Requests;
using TrustSimBackend.Application.Features.System.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.System.Queries;

public class GetMyNotificationsQueryHandler : IRequestHandler<GetMyNotificationsQuery, PagedResult<NotificationDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetMyNotificationsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<NotificationDto>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _unitOfWork.Notifications.GetPagedByUserIdAsync(
            request.UserId,
            request.IsRead,
            request.PageIndex,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(n => new NotificationDto
        {
            Id = n.Id,
            UserId = n.UserId,
            Title = n.Title,
            Content = n.Content,
            NotificationType = n.NotificationType,
            ReferenceType = n.ReferenceType,
            ReferenceId = n.ReferenceId,
            MetadataJson = n.MetadataJson,
            ActionUrl = n.ActionUrl,
            IsRead = n.IsRead,
            ReadAt = n.ReadAt,
            CreatedDate = n.CreatedDate
        }).ToList();

        return PagedResult<NotificationDto>.Create(dtos, totalCount, request.PageIndex, request.PageSize);
    }
}
