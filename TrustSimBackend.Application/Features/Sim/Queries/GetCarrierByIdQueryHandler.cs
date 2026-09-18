using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class GetCarrierByIdQueryHandler : IRequestHandler<GetCarrierByIdQuery, SimCarrierDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetCarrierByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<SimCarrierDto> Handle(GetCarrierByIdQuery request, CancellationToken cancellationToken)
    {
        var carrier = await _unitOfWork.SimCarriers.GetByIdAsync(request.Id, cancellationToken);
        if (carrier == null)
        {
            throw new NotFoundException($"Không tìm thấy nhà mạng với ID '{request.Id}'.");
        }

        return new SimCarrierDto
        {
            Id = carrier.Id,
            CarrierName = carrier.CarrierName,
            CarrierCode = carrier.CarrierCode,
            LogoImg = carrier.LogoImg
        };
    }
}
