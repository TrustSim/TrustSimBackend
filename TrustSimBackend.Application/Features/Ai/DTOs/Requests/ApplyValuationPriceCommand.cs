using MediatR;

namespace TrustSimBackend.Application.Features.Ai.DTOs.Requests;

public record ApplyValuationPriceCommand(
    Guid ValuationId,
    Guid CurrentUserId
) : IRequest<bool>;
