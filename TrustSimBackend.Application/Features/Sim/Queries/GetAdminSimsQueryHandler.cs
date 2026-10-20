using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class GetAdminSimsQueryHandler : IRequestHandler<GetAdminSimsQuery, PagedResult<SimListItemDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetAdminSimsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<SimListItemDto>> Handle(GetAdminSimsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _unitOfWork.Sims.GetAdminSimsPagedAsync(
            request.Filter,
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
                Price = sim.Price,
                CarrierName = sim.PhonePrefix?.SimCarrier?.CarrierName ?? string.Empty,
                CarrierCode = sim.PhonePrefix?.SimCarrier?.CarrierCode ?? string.Empty,
                CarrierLogo = sim.PhonePrefix?.SimCarrier?.LogoImg ?? string.Empty,
                PrefixValue = sim.PhonePrefix?.PrefixValue ?? string.Empty,
                FormatRhythmId = sim.FormatRhythmId,
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
            request.Filter.PageIndex,
            request.Filter.PageSize);

        return result;
    }
}
