using MediatR;
using TrustSimBackend.Application.Features.Ai.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Ai.DTOs.Requests;

public record GetConsultationSessionsQuery(Guid UserId) : IRequest<IReadOnlyList<ConsultationSessionDto>>;
