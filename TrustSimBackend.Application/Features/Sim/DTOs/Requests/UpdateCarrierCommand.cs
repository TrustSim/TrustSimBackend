using MediatR;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public record UpdateCarrierCommand(
    Guid Id = default,
    string CarrierName = "",
    string? LogoImg = null
) : IRequest<SimCarrierDto>;
