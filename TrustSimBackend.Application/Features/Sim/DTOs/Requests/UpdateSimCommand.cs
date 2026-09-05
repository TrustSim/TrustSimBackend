using MediatR;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public record UpdateSimCommand(
    Guid Id = default,
    SimStatus SimStatus = default,
    Guid PhonePrefixId = default,
    Guid? FormatRhythmId = null,
    List<Guid>? CategoryIds = null
) : IRequest<SimNumberDto>;
