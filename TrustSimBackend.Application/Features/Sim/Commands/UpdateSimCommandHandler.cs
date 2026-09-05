using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class UpdateSimCommandHandler : IRequestHandler<UpdateSimCommand, SimNumberDto>
{
    private readonly ISimNumberRepository _simRepository;
    private readonly IPhonePrefixRepository _prefixRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateSimCommandHandler(
        ISimNumberRepository simRepository,
        IPhonePrefixRepository prefixRepository,
        IUnitOfWork unitOfWork)
    {
        _simRepository = simRepository;
        _prefixRepository = prefixRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<SimNumberDto> Handle(UpdateSimCommand request, CancellationToken cancellationToken)
    {
        var sim = await _simRepository.GetByIdAsync(request.Id, cancellationToken);
        if (sim == null)
        {
            throw new NotFoundException($"Không tìm thấy số SIM với ID '{request.Id}'.");
        }

        var prefix = await _prefixRepository.GetByIdAsync(request.PhonePrefixId, cancellationToken);
        if (prefix == null)
        {
            throw new NotFoundException($"Không tìm thấy đầu số với ID '{request.PhonePrefixId}'.");
        }

        var rhythmId = request.FormatRhythmId.HasValue && request.FormatRhythmId.Value != Guid.Empty
            ? request.FormatRhythmId.Value
            : sim.FormatRhythmId;

        sim.SimStatus = request.SimStatus;
        sim.PhonePrefixId = request.PhonePrefixId;
        sim.FormatRhythmId = rhythmId;

        sim.SimCategoryMappings.Clear();
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

        _simRepository.Update(sim);
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
