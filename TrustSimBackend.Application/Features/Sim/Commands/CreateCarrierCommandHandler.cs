using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class CreateCarrierCommandHandler : IRequestHandler<CreateCarrierCommand, SimCarrierDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateCarrierCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<SimCarrierDto> Handle(CreateCarrierCommand request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.CarrierCode.Trim().ToUpperInvariant();
        if (await _unitOfWork.SimCarriers.ExistsByCodeAsync(normalizedCode, cancellationToken: cancellationToken))
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

        await _unitOfWork.SimCarriers.AddAsync(carrier, cancellationToken);
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
