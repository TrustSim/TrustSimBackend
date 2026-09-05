using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class GetSimByIdQueryHandler : IRequestHandler<GetSimByIdQuery, SimNumberDto>
{
    private readonly ISimNumberRepository _simRepository;

    public GetSimByIdQueryHandler(ISimNumberRepository simRepository)
    {
        _simRepository = simRepository;
    }

    public async Task<SimNumberDto> Handle(GetSimByIdQuery request, CancellationToken cancellationToken)
    {
        var sim = await _simRepository.GetByIdAsync(request.Id, cancellationToken);
        if (sim == null)
        {
            throw new NotFoundException($"Không tìm thấy số SIM với ID '{request.Id}'.");
        }

        return new SimNumberDto
        {
            Id = sim.Id,
            PhoneNumber = sim.PhoneNumber,
            SimStatus = sim.SimStatus,
            PhonePrefixId = sim.PhonePrefixId,
            PrefixValue = sim.PhonePrefix?.PrefixValue ?? string.Empty,
            CarrierName = sim.PhonePrefix?.SimCarrier?.CarrierName ?? string.Empty,
            CarrierLogo = sim.PhonePrefix?.SimCarrier?.LogoImg ?? string.Empty,
            FormatRhythmId = sim.FormatRhythmId,
            CategoryNames = sim.SimCategoryMappings.Select(m => m.SimCategory?.CategoryName ?? string.Empty).Where(x => !string.IsNullOrEmpty(x)).ToList(),
            CreatedDate = sim.CreatedDate
        };
    }
}
