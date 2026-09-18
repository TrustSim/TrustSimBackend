using MediatR;
using TrustSimBackend.Application.Features.Ai.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Ai.DTOs.Requests;

public record SendConsultationMessageCommand(
    ChatConsultationRequest Request,
    Guid? CurrentUserId
) : IRequest<ChatConsultationResponseDto>;
