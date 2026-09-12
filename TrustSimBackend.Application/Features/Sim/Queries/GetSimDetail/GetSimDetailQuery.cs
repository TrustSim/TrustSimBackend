using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries.GetSimDetail;

/// <summary>
/// Query lấy thông tin chi tiết một số SIM theo ID.
/// </summary>
public record GetSimDetailQuery(Guid Id) : IRequest<ApiResponse<SimDetailDto>>;

public class GetSimDetailQueryHandler : IRequestHandler<GetSimDetailQuery, ApiResponse<SimDetailDto>>
{
    private readonly ISimRepository _simRepository;

    public GetSimDetailQueryHandler(ISimRepository simRepository)
    {
        _simRepository = simRepository;
    }

    public async Task<ApiResponse<SimDetailDto>> Handle(GetSimDetailQuery request, CancellationToken cancellationToken)
    {
        var sim = await _simRepository.GetDetailByIdAsync(request.Id, cancellationToken);
        if (sim == null)
        {
            throw new NotFoundException($"Không tìm thấy số SIM với mã định danh '{request.Id}'.");
        }

        var activeOwnership = sim.SimOwnerships.FirstOrDefault(o => o.OwnershipStatus == OwnershipStatus.ACTIVE);
        var latestVerification = activeOwnership?.SimVerifications.OrderByDescending(v => v.RequestedAt).FirstOrDefault();
        var owner = activeOwnership?.UserAccount;

        var dto = new SimDetailDto
        {
            Id = sim.Id,
            PhoneNumber = sim.PhoneNumber,
            SimStatus = sim.SimStatus,
            PhonePrefixId = sim.PhonePrefixId,
            PrefixValue = sim.PhonePrefix?.PrefixValue ?? string.Empty,
            CarrierId = sim.PhonePrefix?.SimCarrierId ?? Guid.Empty,
            CarrierName = sim.PhonePrefix?.SimCarrier?.CarrierName ?? string.Empty,
            CarrierCode = sim.PhonePrefix?.SimCarrier?.CarrierCode ?? string.Empty,
            CarrierLogo = sim.PhonePrefix?.SimCarrier?.LogoImg ?? string.Empty,
            FormatRhythmId = sim.FormatRhythmId,
            FormatRhythmName = sim.FormatRhythm?.FormatRhythmName ?? string.Empty,
            FormatRhythmValue = sim.FormatRhythm?.Value ?? string.Empty,
            Categories = sim.SimCategoryMappings
                .Where(m => m.SimCategory != null && m.SimCategory.IsActive)
                .Select(m => new SimCategoryDto
                {
                    Id = m.SimCategory!.Id,
                    CategoryCode = m.SimCategory.CategoryCode,
                    CategoryName = m.SimCategory.CategoryName,
                    Description = m.SimCategory.Description,
                    ValuationMultiplier = m.SimCategory.ValuationMultiplier,
                    IsActive = m.SimCategory.IsActive
                }).ToList(),
            OwnerId = owner?.Id,
            OwnerUserName = owner?.UserName,
            OwnerFullName = owner?.UserProfile?.FullName,
            OwnerEmail = owner?.UserEmail,
            OwnerPhoneNumber = owner?.UserProfile?.PhoneNumber,
            OwnershipStatus = activeOwnership?.OwnershipStatus,
            VerificationStatus = latestVerification?.VerificationStatus,
            RejectionReason = latestVerification?.RejectionReason,
            CreatedDate = sim.CreatedDate,
            UpdatedDate = sim.UpdatedDate
        };

        return ApiResponse<SimDetailDto>.SuccessResult(dto, "Lấy thông tin chi tiết số SIM thành công.");
    }
}
