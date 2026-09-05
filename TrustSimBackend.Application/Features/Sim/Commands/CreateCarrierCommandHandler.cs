using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class CreateCarrierCommandHandler : IRequestHandler<CreateCarrierCommand, SimCarrierDto>
{
    private readonly ISimCarrierRepository _carrierRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCarrierCommandHandler(ISimCarrierRepository carrierRepository, IUnitOfWork unitOfWork)
    {
        _carrierRepository = carrierRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<SimCarrierDto> Handle(CreateCarrierCommand request, CancellationToken cancellationToken)
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

        return new SimCarrierDto
        {
            Id = carrier.Id,
            CarrierName = carrier.CarrierName,
            CarrierCode = carrier.CarrierCode,
            LogoImg = carrier.LogoImg
        };
    }
}
