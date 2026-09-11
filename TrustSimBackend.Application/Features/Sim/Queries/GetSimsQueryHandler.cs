using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class GetSimsQueryHandler : IRequestHandler<GetSimsQuery, PagedResult<SimNumberDto>>
{
    private readonly ISimNumberRepository _simRepository;

    public GetSimsQueryHandler(ISimNumberRepository simRepository)
    {
        _simRepository = simRepository;
    }

    public async Task<PagedResult<SimNumberDto>> Handle(GetSimsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _simRepository.GetPagedAsync(
            request.Keyword,
            request.CarrierId,
            request.CategoryId,
            request.PrefixId,
            request.Status,
            request.PageIndex,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(s => new SimNumberDto
        {
            Id = s.Id,
            PhoneNumber = s.PhoneNumber,
            SimStatus = s.SimStatus,
            PhonePrefixId = s.PhonePrefixId,
            PrefixValue = s.PhonePrefix?.PrefixValue ?? string.Empty,
            CarrierName = s.PhonePrefix?.SimCarrier?.CarrierName ?? string.Empty,
            CarrierLogo = s.PhonePrefix?.SimCarrier?.LogoImg ?? string.Empty,
            FormatRhythmId = s.FormatRhythmId,
            CategoryNames = s.SimCategoryMappings.Select(m => m.SimCategory?.CategoryName ?? string.Empty).Where(x => !string.IsNullOrEmpty(x)).ToList(),
            CreatedDate = s.CreatedDate
        }).ToList();

        return PagedResult<SimNumberDto>.Create(dtos, totalCount, request.PageIndex, request.PageSize);
    }
}
