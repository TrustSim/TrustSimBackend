using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.System.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.System.Commands;

public class MarkNotificationAsReadCommandHandler : IRequestHandler<MarkNotificationAsReadCommand, bool>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public MarkNotificationAsReadCommandHandler(INotificationRepository notificationRepository, IUnitOfWork unitOfWork)
    {
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await _notificationRepository.GetByIdAsync(request.NotificationId, cancellationToken);
        if (notification == null)
        {
            throw new NotFoundException($"Không tìm thấy thông báo với ID '{request.NotificationId}'.");
        }

        if (notification.UserId != request.UserId)
        {
            throw new ForbiddenAccessException("Bạn không có quyền thao tác trên thông báo này.");
        }

        notification.IsRead = true;
        _notificationRepository.Update(notification);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
