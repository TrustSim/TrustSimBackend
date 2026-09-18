using MediatR;
using TrustSimBackend.Application.Features.Ai.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Ai.DTOs.Requests;

public record GetConsultationMessagesQuery(Guid SessionId) : IRequest<IReadOnlyList<ConsultationMessageDto>>;
