using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands.Carriers;

public record UpdateCarrierCommand(
    Guid Id,
    string CarrierName,
    string? LogoImg
) : IRequest<ApiResponse<SimCarrierDto>>;

public class UpdateCarrierCommandHandler : IRequestHandler<UpdateCarrierCommand, ApiResponse<SimCarrierDto>>
{
    private readonly ISimCarrierRepository _carrierRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCarrierCommandHandler(ISimCarrierRepository carrierRepository, IUnitOfWork unitOfWork)
    {
        _carrierRepository = carrierRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<SimCarrierDto>> Handle(UpdateCarrierCommand request, CancellationToken cancellationToken)
    {
        var carrier = await _carrierRepository.GetByIdAsync(request.Id, cancellationToken);
        if (carrier == null)
        {
            throw new NotFoundException($"Không tìm thấy nhà mạng với ID '{request.Id}'.");
        }

        carrier.CarrierName = request.CarrierName.Trim();
        if (request.LogoImg != null)
        {
            carrier.LogoImg = request.LogoImg.Trim();
        }

        _carrierRepository.Update(carrier);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new SimCarrierDto
        {
            Id = carrier.Id,
            CarrierName = carrier.CarrierName,
            CarrierCode = carrier.CarrierCode,
            LogoImg = carrier.LogoImg
        };

        return ApiResponse<SimCarrierDto>.SuccessResult(dto, "Cập nhật nhà mạng thành công.");
    }
}
