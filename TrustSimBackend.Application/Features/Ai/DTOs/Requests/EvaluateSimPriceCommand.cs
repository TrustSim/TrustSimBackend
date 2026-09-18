using MediatR;
using TrustSimBackend.Application.Features.Ai.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Ai.DTOs.Requests;

public record EvaluateSimPriceCommand(
    Guid? SimNumberId,
    string? PhoneNumber,
    Guid? CurrentUserId
) : IRequest<SimValuationResponseDto>;
