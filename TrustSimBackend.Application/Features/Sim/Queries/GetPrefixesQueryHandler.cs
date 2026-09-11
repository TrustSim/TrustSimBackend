using MediatR;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class GetPrefixesQueryHandler : IRequestHandler<GetPrefixesQuery, IReadOnlyList<PhonePrefixDto>>
{
    private readonly IPhonePrefixRepository _prefixRepository;

    public GetPrefixesQueryHandler(IPhonePrefixRepository prefixRepository)
    {
        _prefixRepository = prefixRepository;
    }

    public async Task<IReadOnlyList<PhonePrefixDto>> Handle(GetPrefixesQuery request, CancellationToken cancellationToken)
    {
        var prefixes = await _prefixRepository.GetAllAsync(request.CarrierId, cancellationToken);
        return prefixes.Select(p => new PhonePrefixDto
        {
            Id = p.Id,
            PrefixValue = p.PrefixValue,
            SimCarrierId = p.SimCarrierId,
            CarrierName = p.SimCarrier?.CarrierName ?? string.Empty
        }).ToList();
    }
}
