using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries.Carriers;

public record GetCarriersQuery() : IRequest<ApiResponse<IReadOnlyList<SimCarrierDto>>>;

public class GetCarriersQueryHandler : IRequestHandler<GetCarriersQuery, ApiResponse<IReadOnlyList<SimCarrierDto>>>
{
    private readonly ISimCarrierRepository _carrierRepository;

    public GetCarriersQueryHandler(ISimCarrierRepository carrierRepository)
    {
        _carrierRepository = carrierRepository;
    }

    public async Task<ApiResponse<IReadOnlyList<SimCarrierDto>>> Handle(GetCarriersQuery request, CancellationToken cancellationToken)
    {
        var carriers = await _carrierRepository.GetAllAsync(cancellationToken);
        var dtos = carriers.Select(c => new SimCarrierDto
        {
            Id = c.Id,
            CarrierName = c.CarrierName,
            CarrierCode = c.CarrierCode,
            LogoImg = c.LogoImg
        }).ToList();

        return ApiResponse<IReadOnlyList<SimCarrierDto>>.SuccessResult(dtos, "Lấy danh sách nhà mạng thành công.");
    }
}

public record GetCarrierByIdQuery(Guid Id) : IRequest<ApiResponse<SimCarrierDto>>;

public class GetCarrierByIdQueryHandler : IRequestHandler<GetCarrierByIdQuery, ApiResponse<SimCarrierDto>>
{
    private readonly ISimCarrierRepository _carrierRepository;

    public GetCarrierByIdQueryHandler(ISimCarrierRepository carrierRepository)
    {
        _carrierRepository = carrierRepository;
    }

    public async Task<ApiResponse<SimCarrierDto>> Handle(GetCarrierByIdQuery request, CancellationToken cancellationToken)
    {
        var carrier = await _carrierRepository.GetByIdAsync(request.Id, cancellationToken);
        if (carrier == null)
        {
            throw new NotFoundException($"Không tìm thấy nhà mạng với ID '{request.Id}'.");
        }

        var dto = new SimCarrierDto
        {
            Id = carrier.Id,
            CarrierName = carrier.CarrierName,
            CarrierCode = carrier.CarrierCode,
            LogoImg = carrier.LogoImg,
            Prefixes = carrier.PhonePrefixes.Select(p => new PhonePrefixDto
            {
                Id = p.Id,
                PrefixValue = p.PrefixValue,
                SimCarrierId = p.SimCarrierId,
                CarrierName = carrier.CarrierName
            }).ToList()
        };

        return ApiResponse<SimCarrierDto>.SuccessResult(dto, "Lấy thông tin nhà mạng thành công.");
    }
}
