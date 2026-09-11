using MediatR;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public record DeleteFormatRhythmCommand(Guid Id) : IRequest<bool>;
