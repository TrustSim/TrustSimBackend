using MediatR;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public record CreateCategoryCommand(
    string CategoryCode,
    string CategoryName,
    string? Description = null,
    float ValuationMultiplier = 1.0f
) : IRequest<SimCategoryDto>;
