using MediatR;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Queries;

/// <summary>
/// Handler lấy lịch sử tất cả các lần nộp hồ sơ KYC của người dùng.
/// </summary>
public class GetMyKycHistoryQueryHandler : IRequestHandler<GetMyKycHistoryQuery, IReadOnlyList<IdentityVerificationDto>>
{
    private readonly IIdentityVerificationRepository _kycRepository;

    public GetMyKycHistoryQueryHandler(IIdentityVerificationRepository kycRepository)
    {
        _kycRepository = kycRepository;
    }

    public async Task<IReadOnlyList<IdentityVerificationDto>> Handle(GetMyKycHistoryQuery request, CancellationToken cancellationToken)
    {
        var list = await _kycRepository.GetHistoryByUserIdAsync(request.UserId, cancellationToken);

        return list.Select(v => new IdentityVerificationDto
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
    }
}
