using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class CreateSimCommandHandler : IRequestHandler<CreateSimCommand, SimNumberDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateSimCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<SimNumberDto> Handle(CreateSimCommand request, CancellationToken cancellationToken)
    {
        var cleanNumber = request.PhoneNumber.Trim();
        if (await _unitOfWork.SimNumbers.ExistsByPhoneNumberAsync(cleanNumber, cancellationToken: cancellationToken))
        {
            throw new BadRequestException($"Số SIM '{cleanNumber}' đã tồn tại trong hệ thống.");
        }

        var prefix = await _unitOfWork.PhonePrefixes.GetByIdAsync(request.PhonePrefixId, cancellationToken);
        if (prefix == null)
        {
            throw new NotFoundException($"Không tìm thấy đầu số với ID '{request.PhonePrefixId}'.");
        }

        var rhythmId = request.FormatRhythmId.HasValue && request.FormatRhythmId.Value != Guid.Empty
            ? request.FormatRhythmId.Value
            : await _unitOfWork.SimNumbers.GetOrCreateDefaultFormatRhythmIdAsync(cancellationToken);

        var sim = new SimNumber
        {
            Id = Guid.NewGuid(),
            PhoneNumber = cleanNumber,
            SimStatus = SimStatus.AVAILABLE,
            PhonePrefixId = request.PhonePrefixId,
            FormatRhythmId = rhythmId
        };

        if (request.CategoryIds != null && request.CategoryIds.Any())
        {
            foreach (var catId in request.CategoryIds.Distinct())
            {
                sim.SimCategoryMappings.Add(new SimCategoryMapping
                {
                    Id = Guid.NewGuid(),
                    SimNumberId = sim.Id,
                    SimCategoryId = catId
                });
            }
        }

        await _unitOfWork.SimNumbers.AddAsync(sim, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SimNumberDto
        {
            Id = sim.Id,
            PhoneNumber = sim.PhoneNumber,
            SimStatus = sim.SimStatus,
            PhonePrefixId = sim.PhonePrefixId,
            PrefixValue = prefix.PrefixValue,
            CarrierName = prefix.SimCarrier?.CarrierName ?? string.Empty,
            CarrierLogo = prefix.SimCarrier?.LogoImg ?? string.Empty,
            FormatRhythmId = sim.FormatRhythmId,
            CreatedDate = sim.CreatedDate
        };
    }
}
