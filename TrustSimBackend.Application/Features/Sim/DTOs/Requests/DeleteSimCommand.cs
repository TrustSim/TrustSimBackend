using MediatR;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public record DeleteSimCommand(Guid Id) : IRequest<bool>;
