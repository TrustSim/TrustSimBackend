using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class UpdateCarrierCommandHandler : IRequestHandler<UpdateCarrierCommand, SimCarrierDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCarrierCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<SimCarrierDto> Handle(UpdateCarrierCommand request, CancellationToken cancellationToken)
    {
        var carrier = await _unitOfWork.SimCarriers.GetByIdAsync(request.Id, cancellationToken);
        if (carrier == null)
        {
            throw new NotFoundException($"Không tìm thấy nhà mạng với ID '{request.Id}'.");
        }

        carrier.CarrierName = request.CarrierName.Trim();
        if (request.LogoImg != null)
        {
            carrier.LogoImg = request.LogoImg.Trim();
        }

        _unitOfWork.SimCarriers.Update(carrier);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SimCarrierDto
        {
            Id = carrier.Id,
            CarrierName = carrier.CarrierName,
            CarrierCode = carrier.CarrierCode,
            LogoImg = carrier.LogoImg
        };
    }
}
