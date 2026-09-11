using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Queries;

/// <summary>
/// Handler lấy chi tiết hồ sơ xác minh KYC (bằng chứng ảnh, kết quả OCR, điểm AWS CompareFaces) cho Admin.
/// </summary>
public class GetKycVerificationDetailQueryHandler : IRequestHandler<GetKycVerificationDetailQuery, IdentityVerificationDetailDto>
{
    private readonly IIdentityVerificationRepository _kycRepository;

    public GetKycVerificationDetailQueryHandler(IIdentityVerificationRepository kycRepository)
    {
        _kycRepository = kycRepository;
    }

    public async Task<IdentityVerificationDetailDto> Handle(GetKycVerificationDetailQuery request, CancellationToken cancellationToken)
    {
        var v = await _kycRepository.GetDetailByIdAsync(request.VerificationId, cancellationToken);
        if (v == null)
        {
            throw new NotFoundException($"Không tìm thấy hồ sơ xác minh KYC với ID '{request.VerificationId}'.");
        }

        return new IdentityVerificationDetailDto
        {
            Id = v.Id,
            UserProfileId = v.UserProfileId,
            UserAccountId = v.UserProfile?.UserAccountId ?? Guid.Empty,
            UserName = v.UserProfile?.UserAccount?.UserName ?? string.Empty,
            UserEmail = v.UserProfile?.UserAccount?.UserEmail ?? string.Empty,
            FullName = v.UserProfile?.FullName ?? string.Empty,
            IdentityNumber = v.UserProfile?.IdentityNumber ?? string.Empty,
            DateOfBirth = v.UserProfile?.DateOfBirth,
            Address = v.UserProfile?.Address ?? string.Empty,
            PhoneNumber = v.UserProfile?.PhoneNumber ?? string.Empty,
            FrontIdCardUrl = v.FrontIdCardUrl,
            BackIdCardUrl = v.BackIdCardUrl,
            FaceSelfieUrl = v.FaceSelfieUrl,
            FaceSimilarityScore = v.FaceSimilarityScore,
            OcrRawData = v.OcrRawData,
            VerificationMethod = v.VerificationMethod,
            VerificationStatus = v.VerificationStatus,
            RejectionReason = v.RejectionReason,
            SubmittedAt = v.SubmittedAt,
            VerifiedAt = v.VerifiedAt,
            VerifiedByUserId = v.VerifiedByUserId,
            VerifiedByUserName = v.VerifiedByUser?.UserName
        };
    }
}
