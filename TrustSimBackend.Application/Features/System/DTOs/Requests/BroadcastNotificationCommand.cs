using MediatR;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.System.DTOs.Requests;

public record BroadcastNotificationCommand(
    string Title,
    string Content,
    string? ActionUrl = null,
    UserRole? TargetRole = null
) : IRequest<int>;
