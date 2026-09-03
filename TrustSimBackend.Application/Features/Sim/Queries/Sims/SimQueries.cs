using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries.Sims;

public record GetSimsQuery(
    string? Keyword = null,
    Guid? CarrierId = null,
    Guid? CategoryId = null,
    Guid? PrefixId = null,
    SimStatus? Status = null,
    int PageIndex = 1,
    int PageSize = 20
) : IRequest<ApiResponse<PagedResult<SimNumberDto>>>;

public class GetSimsQueryHandler : IRequestHandler<GetSimsQuery, ApiResponse<PagedResult<SimNumberDto>>>
{
    private readonly ISimNumberRepository _simRepository;

    public GetSimsQueryHandler(ISimNumberRepository simRepository)
    {
        _simRepository = simRepository;
    }

    public async Task<ApiResponse<PagedResult<SimNumberDto>>> Handle(GetSimsQuery request, CancellationToken cancellationToken)
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

        var pagedResult = PagedResult<SimNumberDto>.Create(dtos, totalCount, request.PageIndex, request.PageSize);
        return ApiResponse<PagedResult<SimNumberDto>>.SuccessResult(pagedResult, "Lấy danh sách SIM thành công.");
    }
}

public record GetSimByIdQuery(Guid Id) : IRequest<ApiResponse<SimNumberDto>>;

public class GetSimByIdQueryHandler : IRequestHandler<GetSimByIdQuery, ApiResponse<SimNumberDto>>
{
    private readonly ISimNumberRepository _simRepository;

    public GetSimByIdQueryHandler(ISimNumberRepository simRepository)
    {
        _simRepository = simRepository;
    }

    public async Task<ApiResponse<SimNumberDto>> Handle(GetSimByIdQuery request, CancellationToken cancellationToken)
    {
        var sim = await _simRepository.GetByIdAsync(request.Id, cancellationToken);
        if (sim == null)
        {
            throw new NotFoundException($"Không tìm thấy số SIM với ID '{request.Id}'.");
        }

        var dto = new SimNumberDto
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

        return ApiResponse<SimNumberDto>.SuccessResult(dto, "Lấy thông tin chi tiết số SIM thành công.");
    }
}
