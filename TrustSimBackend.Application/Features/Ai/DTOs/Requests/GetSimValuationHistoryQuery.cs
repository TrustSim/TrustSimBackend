using MediatR;
using TrustSimBackend.Application.Features.Ai.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Ai.DTOs.Requests;

public record GetSimValuationHistoryQuery(Guid SimNumberId) : IRequest<IReadOnlyList<SimValuationResponseDto>>;
