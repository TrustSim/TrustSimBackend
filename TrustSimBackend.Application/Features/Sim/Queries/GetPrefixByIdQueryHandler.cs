using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class GetPrefixByIdQueryHandler : IRequestHandler<GetPrefixByIdQuery, PhonePrefixDto>
{
    private readonly IPhonePrefixRepository _prefixRepository;

    public GetPrefixByIdQueryHandler(IPhonePrefixRepository prefixRepository)
    {
        _prefixRepository = prefixRepository;
    }

    public async Task<PhonePrefixDto> Handle(GetPrefixByIdQuery request, CancellationToken cancellationToken)
    {
        var p = await _prefixRepository.GetByIdAsync(request.Id, cancellationToken);
        if (p == null) throw new NotFoundException($"Không tìm thấy đầu số với ID '{request.Id}'.");

        return new PhonePrefixDto
        {
            Id = p.Id,
            PrefixValue = p.PrefixValue,
            SimCarrierId = p.SimCarrierId,
            CarrierName = p.SimCarrier?.CarrierName ?? string.Empty
        };
    }
}
