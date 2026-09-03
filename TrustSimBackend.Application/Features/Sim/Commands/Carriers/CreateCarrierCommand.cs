using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands.Carriers;

public record CreateCarrierCommand(
    string CarrierName,
    string CarrierCode,
    string? LogoImg
) : IRequest<ApiResponse<SimCarrierDto>>;

public class CreateCarrierCommandHandler : IRequestHandler<CreateCarrierCommand, ApiResponse<SimCarrierDto>>
{
    private readonly ISimCarrierRepository _carrierRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCarrierCommandHandler(ISimCarrierRepository carrierRepository, IUnitOfWork unitOfWork)
    {
        _carrierRepository = carrierRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<SimCarrierDto>> Handle(CreateCarrierCommand request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.CarrierCode.Trim().ToUpperInvariant();
        if (await _carrierRepository.ExistsByCodeAsync(normalizedCode, cancellationToken: cancellationToken))
        {
            throw new BadRequestException($"Mã nhà mạng '{normalizedCode}' đã tồn tại trong hệ thống.");
        }

        var carrier = new SimCarrier
        {
            Id = Guid.NewGuid(),
            CarrierName = request.CarrierName.Trim(),
            CarrierCode = normalizedCode,
            LogoImg = request.LogoImg?.Trim() ?? string.Empty
        };

        await _carrierRepository.AddAsync(carrier, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new SimCarrierDto
        {
            Id = carrier.Id,
            CarrierName = carrier.CarrierName,
            CarrierCode = carrier.CarrierCode,
            LogoImg = carrier.LogoImg
        };

        return ApiResponse<SimCarrierDto>.SuccessResult(dto, "Thêm mới nhà mạng thành công.");
    }
}
