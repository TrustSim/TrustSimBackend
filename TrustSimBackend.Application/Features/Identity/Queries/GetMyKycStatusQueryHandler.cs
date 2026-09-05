using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Queries;

/// <summary>
/// Handler lấy trạng thái KYC gần nhất của người dùng hiện tại.
/// </summary>
public class GetMyKycStatusQueryHandler : IRequestHandler<GetMyKycStatusQuery, IdentityVerificationDto>
{
    private readonly IIdentityVerificationRepository _kycRepository;

    public GetMyKycStatusQueryHandler(IIdentityVerificationRepository kycRepository)
    {
        _kycRepository = kycRepository;
    }

    public async Task<IdentityVerificationDto> Handle(GetMyKycStatusQuery request, CancellationToken cancellationToken)
    {
        var verification = await _kycRepository.GetLatestByUserIdAsync(request.UserId, cancellationToken);
        if (verification == null)
        {
            throw new NotFoundException("Bạn chưa nộp hồ sơ xác minh KYC nào.");
        }

        return new IdentityVerificationDto
        {
            Id = verification.Id,
            UserProfileId = verification.UserProfileId,
            UserAccountId = verification.UserProfile?.UserAccountId ?? Guid.Empty,
            UserName = verification.UserProfile?.UserAccount?.UserName ?? string.Empty,
            FullName = verification.UserProfile?.FullName ?? string.Empty,
            IdentityNumber = verification.UserProfile?.IdentityNumber ?? string.Empty,
            VerificationStatus = verification.VerificationStatus,
            RejectionReason = verification.RejectionReason,
            SubmittedAt = verification.SubmittedAt,
            VerifiedAt = verification.VerifiedAt
        };
    }
}
