using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class UpdatePrefixCommandHandler : IRequestHandler<UpdatePrefixCommand, PhonePrefixDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePrefixCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PhonePrefixDto> Handle(UpdatePrefixCommand request, CancellationToken cancellationToken)
    {
        var prefix = await _unitOfWork.PhonePrefixes.GetByIdAsync(request.Id, cancellationToken);
        if (prefix == null) throw new NotFoundException($"Không tìm thấy đầu số với ID '{request.Id}'.");

        var carrier = await _unitOfWork.SimCarriers.GetByIdAsync(request.SimCarrierId, cancellationToken);
        if (carrier == null) throw new NotFoundException($"Không tìm thấy nhà mạng với ID '{request.SimCarrierId}'.");

        prefix.PrefixValue = request.PrefixValue.Trim();
        prefix.SimCarrierId = request.SimCarrierId;
        prefix.UpdatedDate = DateTime.UtcNow;

        _unitOfWork.PhonePrefixes.Update(prefix);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new PhonePrefixDto
        {
            Id = prefix.Id,
            PrefixValue = prefix.PrefixValue,
            SimCarrierId = prefix.SimCarrierId,
            CarrierName = carrier.CarrierName
        };
    }
}
