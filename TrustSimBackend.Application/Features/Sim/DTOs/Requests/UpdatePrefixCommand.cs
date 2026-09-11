using MediatR;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public record UpdatePrefixCommand(Guid Id, string PrefixValue, Guid SimCarrierId) : IRequest<PhonePrefixDto>;
