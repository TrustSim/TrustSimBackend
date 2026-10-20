using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class GetMySimOwnershipsQueryHandler : IRequestHandler<GetMySimOwnershipsQuery, PagedResult<SimOwnershipDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetMySimOwnershipsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<SimOwnershipDto>> Handle(GetMySimOwnershipsQuery request, CancellationToken cancellationToken)
    {
        var (ownerships, totalCount) = await _unitOfWork.SimOwnerships.GetPagedByUserIdAsync(
            request.UserId,
            request.Filter,
            cancellationToken);

        var dtos = ownerships.Select(o =>
        {
            var latestVerification = o.SimVerifications.OrderByDescending(v => v.RequestedAt).FirstOrDefault();
            return new SimOwnershipDto
            {
                Id = o.Id,
                SimNumberId = o.SimNumberId,
                PhoneNumber = o.SimNumber?.PhoneNumber ?? string.Empty,
                Price = o.SimNumber?.Price ?? 0m,
                FormatRhythmId = o.SimNumber?.FormatRhythmId,
                FormatRhythmName = o.SimNumber?.FormatRhythm?.FormatRhythmName,
                SimStatus = o.SimNumber?.SimStatus ?? Domain.Enums.SimStatus.AVAILABLE,
                CarrierName = o.SimNumber?.PhonePrefix?.SimCarrier?.CarrierName ?? string.Empty,
                CarrierCode = o.SimNumber?.PhonePrefix?.SimCarrier?.CarrierCode ?? string.Empty,
                CarrierLogo = o.SimNumber?.PhonePrefix?.SimCarrier?.LogoImg ?? string.Empty,
                OwnershipStatus = o.OwnershipStatus,
                VerificationStatus = latestVerification?.VerificationStatus ?? Domain.Enums.VerificationStatus.PENDING_VERIFICATION,
                VerifiedAt = latestVerification?.VerifiedAt,
                CreatedDate = o.CreatedDate
            };
        }).ToList();

        return PagedResult<SimOwnershipDto>.Create(dtos, totalCount, request.Filter.PageIndex, request.Filter.PageSize);
    }
}
