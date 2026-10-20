using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Queries;

/// <summary>
/// Handler lấy lịch sử tất cả các lần nộp hồ sơ KYC của người dùng (phân trang, sắp xếp).
/// </summary>
public class GetMyKycHistoryQueryHandler : IRequestHandler<GetMyKycHistoryQuery, PagedResult<IdentityVerificationDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetMyKycHistoryQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<IdentityVerificationDto>> Handle(GetMyKycHistoryQuery request, CancellationToken cancellationToken)
    {
        var (list, totalCount) = await _unitOfWork.IdentityVerifications.GetPagedHistoryByUserIdAsync(
            request.UserId,
            request.Filter,
            cancellationToken);

        var dtos = list.Select(v => new IdentityVerificationDto
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
