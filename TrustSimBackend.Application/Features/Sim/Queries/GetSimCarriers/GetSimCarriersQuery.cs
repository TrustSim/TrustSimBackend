using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries.GetSimCarriers;

/// <summary>
/// Query lấy danh sách toàn bộ các nhà mạng viễn thông kèm danh sách đầu số.
/// </summary>
public record GetSimCarriersQuery : IRequest<ApiResponse<List<SimCarrierDto>>>;

public class GetSimCarriersQueryHandler : IRequestHandler<GetSimCarriersQuery, ApiResponse<List<SimCarrierDto>>>
{
    private readonly ISimMasterDataRepository _masterDataRepository;

    public GetSimCarriersQueryHandler(ISimMasterDataRepository masterDataRepository)
    {
        _masterDataRepository = masterDataRepository;
    }

    public async Task<ApiResponse<List<SimCarrierDto>>> Handle(GetSimCarriersQuery request, CancellationToken cancellationToken)
    {
        var carriers = await _masterDataRepository.GetCarriersAsync(cancellationToken);

        var result = carriers.Select(c => new SimCarrierDto
        {
            Id = c.Id,
            CarrierName = c.CarrierName,
            CarrierCode = c.CarrierCode,
            LogoImg = c.LogoImg,
            IsActive = c.IsActive,
            Prefixes = c.PhonePrefixes.Select(p => new PhonePrefixDto
            {
                Id = p.Id,
                PrefixValue = p.PrefixValue,
                SimCarrierId = p.SimCarrierId,
                CarrierName = c.CarrierName,
                IsActive = p.IsActive
            }).OrderBy(p => p.PrefixValue).ToList()
        }).ToList();

        return ApiResponse<List<SimCarrierDto>>.SuccessResult(result, "Lấy danh sách nhà mạng thành công.");
    }
}
