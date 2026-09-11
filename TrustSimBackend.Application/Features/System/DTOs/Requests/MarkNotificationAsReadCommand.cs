using MediatR;

namespace TrustSimBackend.Application.Features.System.DTOs.Requests;

public record MarkNotificationAsReadCommand(
    Guid NotificationId = default,
    Guid UserId = default
) : IRequest<bool>;
