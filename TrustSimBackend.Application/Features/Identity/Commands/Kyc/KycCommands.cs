using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.DTOs;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands.Kyc;

public record SubmitKycCommand(
    Guid UserId,
    string IdentityNumber,
    string? AvatarImg
) : IRequest<ApiResponse<IdentityVerificationDto>>;

public class SubmitKycCommandHandler : IRequestHandler<SubmitKycCommand, ApiResponse<IdentityVerificationDto>>
{
    private readonly IIdentityVerificationRepository _kycRepository;
    private readonly IUserAccountRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SubmitKycCommandHandler(
        IIdentityVerificationRepository kycRepository,
        IUserAccountRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _kycRepository = kycRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<IdentityVerificationDto>> Handle(SubmitKycCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null || user.UserProfile == null)
        {
            throw new NotFoundException($"Không tìm thấy hồ sơ người dùng với ID '{request.UserId}'.");
        }

        user.UserProfile.IdentityNumber = request.IdentityNumber.Trim();
        if (!string.IsNullOrWhiteSpace(request.AvatarImg))
        {
            user.UserProfile.AvatarImg = request.AvatarImg.Trim();
        }
        user.UserProfile.VerificationStatus = VerificationStatus.PENDING_VERIFICATION;

        var verification = new IdentityVerification
        {
            Id = Guid.NewGuid(),
            UserProfileId = user.UserProfile.Id,
            VerificationStatus = VerificationStatus.PENDING_VERIFICATION,
            SubmittedAt = DateTime.UtcNow
        };

        _userRepository.Update(user);
        await _kycRepository.AddAsync(verification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new IdentityVerificationDto
        {
            Id = verification.Id,
            UserProfileId = verification.UserProfileId,
            UserAccountId = user.Id,
            UserName = user.UserName,
            FullName = user.UserProfile.FullName,
            IdentityNumber = user.UserProfile.IdentityNumber,
            VerificationStatus = verification.VerificationStatus,
            SubmittedAt = verification.SubmittedAt
        };

        return ApiResponse<IdentityVerificationDto>.SuccessResult(dto, "Gửi hồ sơ xác minh KYC thành công. Vui lòng chờ phê duyệt.");
    }
}

public record ReviewKycCommand(
    Guid VerificationId,
    VerificationStatus Status,
    string? RejectionReason
) : IRequest<ApiResponse<bool>>;

public class ReviewKycCommandHandler : IRequestHandler<ReviewKycCommand, ApiResponse<bool>>
{
    private readonly IIdentityVerificationRepository _kycRepository;
    private readonly IUserAccountRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReviewKycCommandHandler(
        IIdentityVerificationRepository kycRepository,
        IUserAccountRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _kycRepository = kycRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<bool>> Handle(ReviewKycCommand request, CancellationToken cancellationToken)
    {
        var verification = await _kycRepository.GetByIdAsync(request.VerificationId, cancellationToken);
        if (verification == null)
        {
            throw new NotFoundException($"Không tìm thấy hồ sơ KYC với ID '{request.VerificationId}'.");
        }

        verification.VerificationStatus = request.Status;
        verification.RejectionReason = request.RejectionReason?.Trim();
        verification.VerifiedAt = DateTime.UtcNow;

        if (verification.UserProfile != null)
        {
            verification.UserProfile.VerificationStatus = request.Status;
        }

        _kycRepository.Update(verification);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.SuccessResult(true, "Duyệt hồ sơ xác minh KYC thành công.");
    }
}
