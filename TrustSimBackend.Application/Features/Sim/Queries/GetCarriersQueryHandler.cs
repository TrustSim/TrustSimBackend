using MediatR;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class GetCarriersQueryHandler : IRequestHandler<GetCarriersQuery, IReadOnlyList<SimCarrierDto>>
{
    private readonly ISimCarrierRepository _carrierRepository;

    public GetCarriersQueryHandler(ISimCarrierRepository carrierRepository)
    {
        _carrierRepository = carrierRepository;
    }

    public async Task<IReadOnlyList<SimCarrierDto>> Handle(GetCarriersQuery request, CancellationToken cancellationToken)
    {
        var carriers = await _carrierRepository.GetAllAsync(cancellationToken);
        return carriers.Select(c => new SimCarrierDto
        {
            Id = c.Id,
            CarrierName = c.CarrierName,
            CarrierCode = c.CarrierCode,
            LogoImg = c.LogoImg
        }).ToList();
    }
}
