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
    private readonly IIdentityVerificationRepository _kycRepository;

    public GetPendingKycQueryHandler(IIdentityVerificationRepository kycRepository)
    {
        _kycRepository = kycRepository;
    }

    public async Task<PagedResult<IdentityVerificationDto>> Handle(GetPendingKycQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _kycRepository.GetPagedAsync(
            request.Status,
            request.PageIndex,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(v => new IdentityVerificationDto
        {
            Id = v.Id,
            UserProfileId = v.UserProfileId,
            UserAccountId = v.UserProfile?.UserAccountId ?? Guid.Empty,
            UserName = v.UserProfile?.UserAccount?.UserName ?? string.Empty,
            FullName = v.UserProfile?.FullName ?? string.Empty,
            IdentityNumber = v.UserProfile?.IdentityNumber ?? string.Empty,
            VerificationStatus = v.VerificationStatus,
            RejectionReason = v.RejectionReason,
            SubmittedAt = v.SubmittedAt,
            VerifiedAt = v.VerifiedAt
        }).ToList();

        return PagedResult<IdentityVerificationDto>.Create(dtos, totalCount, request.PageIndex, request.PageSize);
    }
}
