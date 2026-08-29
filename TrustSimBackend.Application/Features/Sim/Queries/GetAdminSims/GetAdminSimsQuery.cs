using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries.GetAdminSims;

/// <summary>
/// Query lấy danh sách toàn bộ kho SIM trên hệ thống dành cho Admin (có phân trang và lọc đa tiêu chí).
/// </summary>
public record GetAdminSimsQuery(
    int PageIndex = 1,
    int PageSize = 10,
    SimStatus? Status = null,
    Guid? CarrierId = null,
    Guid? CategoryId = null,
    string? Search = null
) : IRequest<ApiResponse<PagedResult<SimListItemDto>>>;

public class GetAdminSimsQueryHandler : IRequestHandler<GetAdminSimsQuery, ApiResponse<PagedResult<SimListItemDto>>>
{
    private readonly ISimRepository _simRepository;

    public GetAdminSimsQueryHandler(ISimRepository simRepository)
    {
        _simRepository = simRepository;
    }

    public async Task<ApiResponse<PagedResult<SimListItemDto>>> Handle(GetAdminSimsQuery request, CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : (request.PageSize > 100 ? 100 : request.PageSize);

        var (items, totalCount) = await _simRepository.GetAdminSimsPagedAsync(
            pageIndex,
            pageSize,
            request.Status,
            request.CarrierId,
            request.CategoryId,
            request.Search,
            cancellationToken);

        var dtoList = items.Select(sim =>
        {
            var activeOwnership = sim.SimOwnerships.FirstOrDefault(o => o.OwnershipStatus == OwnershipStatus.ACTIVE);
            var latestVerification = activeOwnership?.SimVerifications.OrderByDescending(v => v.RequestedAt).FirstOrDefault();

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
                OwnerId = activeOwnership?.UserAccountId,
                OwnerFullName = activeOwnership?.UserAccount?.UserProfile?.FullName ?? activeOwnership?.UserAccount?.UserName,
                OwnerPhoneNumber = activeOwnership?.UserAccount?.UserProfile?.PhoneNumber,
                VerificationStatus = latestVerification?.VerificationStatus,
                CreatedDate = sim.CreatedDate
            };
        }).ToList();

        var result = new PagedResult<SimListItemDto>(
            dtoList,
            totalCount,
            pageIndex,
            pageSize);

        return ApiResponse<PagedResult<SimListItemDto>>.SuccessResult(result, "Lấy danh sách kho SIM hệ thống thành công.");
    }
}
