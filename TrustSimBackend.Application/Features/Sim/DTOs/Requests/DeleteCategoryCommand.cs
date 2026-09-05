using MediatR;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public record DeleteCategoryCommand(Guid Id) : IRequest<bool>;
