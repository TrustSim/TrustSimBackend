using MediatR;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public record DeleteCarrierCommand(Guid Id) : IRequest<bool>;
