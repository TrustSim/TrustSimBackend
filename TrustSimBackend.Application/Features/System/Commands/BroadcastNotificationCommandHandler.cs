using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.System.DTOs.Requests;
using TrustSimBackend.Domain.Entities.System;
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
        var (users, _) = await _unitOfWork.Users.GetPagedAsync(
            null,
            request.TargetRole,
            isActive: true,
            pageIndex: 1,
            pageSize: 100000,
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
