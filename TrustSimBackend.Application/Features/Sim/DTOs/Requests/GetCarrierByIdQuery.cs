using MediatR;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public record GetCarrierByIdQuery(Guid Id) : IRequest<SimCarrierDto>;
