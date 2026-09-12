using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries.GetMySims;

/// <summary>
/// Query lấy danh sách kho SIM thuộc quyền sở hữu của Người dùng hiện tại (có phân trang & tìm kiếm).
/// </summary>
public record GetMySimsQuery(
    int PageIndex = 1,
    int PageSize = 10,
    SimStatus? Status = null,
    string? Search = null
) : IRequest<ApiResponse<PagedResult<SimListItemDto>>>
{
    // Được gán tự động từ ClaimsPrincipal trong Controller
    public Guid UserAccountId { get; set; }
}

public class GetMySimsQueryHandler : IRequestHandler<GetMySimsQuery, ApiResponse<PagedResult<SimListItemDto>>>
{
    private readonly ISimRepository _simRepository;

    public GetMySimsQueryHandler(ISimRepository simRepository)
    {
        _simRepository = simRepository;
    }

    public async Task<ApiResponse<PagedResult<SimListItemDto>>> Handle(GetMySimsQuery request, CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : (request.PageSize > 100 ? 100 : request.PageSize);

        var (items, totalCount) = await _simRepository.GetMySimsPagedAsync(
            request.UserAccountId,
            pageIndex,
            pageSize,
            request.Status,
            request.Search,
            cancellationToken);

        var dtoList = items.Select(o =>
        {
            var sim = o.SimNumber!;
            var latestVerification = o.SimVerifications.OrderByDescending(v => v.RequestedAt).FirstOrDefault();

            return new SimListItemDto
            {
                Id = sim.Id,
                PhoneNumber = sim.PhoneNumber,
                SimStatus = sim.SimStatus,
                CarrierName = sim.PhonePrefix?.SimCarrier?.CarrierName ?? string.Empty,
                CarrierCode = sim.PhonePrefix?.SimCarrier?.CarrierCode ?? string.Empty,
                CarrierLogo = sim.PhonePrefix?.SimCarrier?.LogoImg ?? string.Empty,
                PrefixValue = sim.PhonePrefix?.PrefixValue ?? string.Empty,
                FormatRhythmName = sim.FormatRhythm?.FormatRhythmName ?? string.Empty,
                CategoryNames = sim.SimCategoryMappings
                    .Where(m => m.SimCategory != null && m.SimCategory.IsActive)
                    .Select(m => m.SimCategory!.CategoryName)
                    .ToList(),
                OwnerId = o.UserAccountId,
                VerificationStatus = latestVerification?.VerificationStatus,
                CreatedDate = o.CreatedDate
            };
        }).ToList();

        var result = new PagedResult<SimListItemDto>(
            dtoList,
            totalCount,
            pageIndex,
            pageSize);

        return ApiResponse<PagedResult<SimListItemDto>>.SuccessResult(result, "Lấy danh sách SIM của bạn thành công.");
    }
}
