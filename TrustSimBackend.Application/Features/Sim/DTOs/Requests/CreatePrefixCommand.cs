using MediatR;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public record CreatePrefixCommand(
    string PrefixValue,
    Guid SimCarrierId
) : IRequest<Responses.PhonePrefixDto>;
