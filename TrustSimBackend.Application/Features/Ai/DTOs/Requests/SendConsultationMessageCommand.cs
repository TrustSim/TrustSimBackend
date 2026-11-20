using MediatR;
using TrustSimBackend.Application.Features.Ai.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Ai.DTOs.Requests;

public record SendConsultationMessageCommand(
    ChatConsultationRequest Request,
    Guid? CurrentUserId,
    string? CurrentUserRole = null,
    string? ClientIp = null
) : IRequest<ChatConsultationResponseDto>;
