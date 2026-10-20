using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.System.DTOs.Requests;
using TrustSimBackend.Domain.Entities.System;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.System.Commands;

public class BroadcastNotificationCommandHandler : IRequestHandler<BroadcastNotificationCommand, int>
{
    private readonly IBulkDataService _bulkDataService;
    private readonly IUnitOfWork _unitOfWork;

    public BroadcastNotificationCommandHandler(
        IBulkDataService bulkDataService,
        IUnitOfWork unitOfWork)
    {
        _bulkDataService = bulkDataService;
        _unitOfWork = unitOfWork;
    }

    public async Task<int> Handle(BroadcastNotificationCommand request, CancellationToken cancellationToken)
    {
        var filter = new AdminUsersQueryFilter
        {
            Role = request.TargetRole,
            IsActive = true,
            PageIndex = 1,
            PageSize = 100000
        };

        var (users, _) = await _unitOfWork.Users.GetPagedAsync(
            filter,
            cancellationToken: cancellationToken);

        if (!users.Any()) return 0;

        var notifications = users.Select(u => new Notification
        {
            Id = Guid.NewGuid(),
            UserId = u.Id,
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            ActionUrl = request.ActionUrl,
            IsRead = false,
            CreatedDate = DateTime.UtcNow
        }).ToList();

        await _bulkDataService.BulkInsertAsync(notifications, cancellationToken: cancellationToken);
        return notifications.Count;
    }
}
