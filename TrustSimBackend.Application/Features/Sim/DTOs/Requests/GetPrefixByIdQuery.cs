using MediatR;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public record GetPrefixByIdQuery(Guid Id) : IRequest<Responses.PhonePrefixDto>;
