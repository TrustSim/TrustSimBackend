using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.DTOs;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Queries.Kyc;

public record GetPendingKycQuery(
    VerificationStatus? Status = null,
    int PageIndex = 1,
    int PageSize = 20
) : IRequest<ApiResponse<PagedResult<IdentityVerificationDto>>>;

public class GetPendingKycQueryHandler : IRequestHandler<GetPendingKycQuery, ApiResponse<PagedResult<IdentityVerificationDto>>>
{
    private readonly IIdentityVerificationRepository _kycRepository;

    public GetPendingKycQueryHandler(IIdentityVerificationRepository kycRepository)
    {
        _kycRepository = kycRepository;
    }

    public async Task<ApiResponse<PagedResult<IdentityVerificationDto>>> Handle(GetPendingKycQuery request, CancellationToken cancellationToken)
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

        var pagedResult = PagedResult<IdentityVerificationDto>.Create(dtos, totalCount, request.PageIndex, request.PageSize);
        return ApiResponse<PagedResult<IdentityVerificationDto>>.SuccessResult(pagedResult, "Lấy danh sách hồ sơ KYC thành công.");
    }
}

public record GetMyKycStatusQuery(Guid UserId) : IRequest<ApiResponse<IdentityVerificationDto>>;

public class GetMyKycStatusQueryHandler : IRequestHandler<GetMyKycStatusQuery, ApiResponse<IdentityVerificationDto>>
{
    private readonly IIdentityVerificationRepository _kycRepository;

    public GetMyKycStatusQueryHandler(IIdentityVerificationRepository kycRepository)
    {
        _kycRepository = kycRepository;
    }

    public async Task<ApiResponse<IdentityVerificationDto>> Handle(GetMyKycStatusQuery request, CancellationToken cancellationToken)
    {
        var verification = await _kycRepository.GetLatestByUserIdAsync(request.UserId, cancellationToken);
        if (verification == null)
        {
            throw new NotFoundException("Bạn chưa nộp hồ sơ xác minh KYC nào.");
        }

        var dto = new IdentityVerificationDto
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

        return ApiResponse<IdentityVerificationDto>.SuccessResult(dto, "Lấy thông tin trạng thái KYC thành công.");
    }
}
