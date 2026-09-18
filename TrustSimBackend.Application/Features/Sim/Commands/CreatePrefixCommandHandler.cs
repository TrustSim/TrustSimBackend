using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class CreatePrefixCommandHandler : IRequestHandler<CreatePrefixCommand, PhonePrefixDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreatePrefixCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PhonePrefixDto> Handle(CreatePrefixCommand request, CancellationToken cancellationToken)
    {
        var cleanValue = request.PrefixValue.Trim();
        if (await _unitOfWork.PhonePrefixes.ExistsByValueAsync(cleanValue, cancellationToken: cancellationToken))
        {
            throw new BadRequestException($"Đầu số '{cleanValue}' đã tồn tại trong hệ thống.");
        }

        var carrier = await _unitOfWork.SimCarriers.GetByIdAsync(request.SimCarrierId, cancellationToken);
        if (carrier == null)
        {
            throw new NotFoundException($"Không tìm thấy nhà mạng với ID '{request.SimCarrierId}'.");
        }

        var prefix = new PhonePrefix
        {
            Id = Guid.NewGuid(),
            PrefixValue = cleanValue,
            SimCarrierId = request.SimCarrierId
        };

        await _unitOfWork.PhonePrefixes.AddAsync(prefix, cancellationToken);
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
