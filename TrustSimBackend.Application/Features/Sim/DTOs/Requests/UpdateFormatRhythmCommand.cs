using MediatR;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public record UpdateFormatRhythmCommand(
    Guid Id,
    string FormatRhythmName,
    string Value,
    string Description
) : IRequest<FormatRhythmDto>;
