using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Handler xử lý nộp hồ sơ KYC.
/// </summary>
public class SubmitKycCommandHandler : IRequestHandler<SubmitKycCommand, IdentityVerificationDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public SubmitKycCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IdentityVerificationDto> Handle(SubmitKycCommand request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
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

        _unitOfWork.Users.Update(user);
        await _unitOfWork.IdentityVerifications.AddAsync(verification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new IdentityVerificationDto
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
    }
}
