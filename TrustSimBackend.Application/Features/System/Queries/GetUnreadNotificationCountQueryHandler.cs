using MediatR;
using TrustSimBackend.Application.Features.System.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.System.Queries;

public class GetUnreadNotificationCountQueryHandler : IRequestHandler<GetUnreadNotificationCountQuery, int>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetUnreadNotificationCountQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<int> Handle(GetUnreadNotificationCountQuery request, CancellationToken cancellationToken)
    {
        return await _unitOfWork.Notifications.GetUnreadCountAsync(request.UserId, cancellationToken);
    }
}
