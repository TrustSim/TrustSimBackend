using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Queries;

/// <summary>
/// Handler lấy danh sách hồ sơ KYC chờ duyệt.
/// </summary>
public class GetPendingKycQueryHandler : IRequestHandler<GetPendingKycQuery, PagedResult<IdentityVerificationDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetPendingKycQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<IdentityVerificationDto>> Handle(GetPendingKycQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _unitOfWork.IdentityVerifications.GetPagedAsync(
            request.Filter,
            cancellationToken);

        var dtos = items.Select(v => new IdentityVerificationDto
        {
            Id = v.Id,
            UserProfileId = v.UserProfileId,
            UserAccountId = v.UserProfile?.UserAccountId ?? Guid.Empty,
            UserName = v.UserProfile?.UserAccount?.UserName ?? string.Empty,
            FullName = v.UserProfile?.FullName ?? string.Empty,
            IdentityNumber = v.UserProfile?.IdentityNumber ?? string.Empty,
            FrontIdCardUrl = v.FrontIdCardUrl,
            BackIdCardUrl = v.BackIdCardUrl,
            FaceSelfieUrl = v.FaceSelfieUrl,
            FaceSimilarityScore = v.FaceSimilarityScore,
            VerificationMethod = v.VerificationMethod,
            VerificationStatus = v.VerificationStatus,
            RejectionReason = v.RejectionReason,
            SubmittedAt = v.SubmittedAt,
            VerifiedAt = v.VerifiedAt,
            VerifiedByUserName = v.VerifiedByUser?.UserName
        }).ToList();

        return PagedResult<IdentityVerificationDto>.Create(dtos, totalCount, request.Filter.PageIndex, request.Filter.PageSize);
    }
}
