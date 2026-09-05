using MediatR;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public record CreateCarrierCommand(
    string CarrierName,
    string CarrierCode,
    string? LogoImg = null
) : IRequest<SimCarrierDto>;
