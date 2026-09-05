using MediatR;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public record CreateSimCommand(
    string PhoneNumber,
    Guid PhonePrefixId,
    Guid? FormatRhythmId = null,
    List<Guid>? CategoryIds = null
) : IRequest<SimNumberDto>;
