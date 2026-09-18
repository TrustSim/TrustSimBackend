using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Handler xử lý phê duyệt hoặc từ chối hồ sơ KYC từ Admin và tự động kích hoạt cặp khóa ký số PKI.
/// </summary>
public class ReviewKycCommandHandler : IRequestHandler<ReviewKycCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPkiKeyService _pkiKeyService;

    public ReviewKycCommandHandler(
        IUnitOfWork unitOfWork,
        IPkiKeyService pkiKeyService)
    {
        _unitOfWork = unitOfWork;
        _pkiKeyService = pkiKeyService;
    }

    public async Task<bool> Handle(ReviewKycCommand request, CancellationToken cancellationToken)
    {
        var verification = await _unitOfWork.IdentityVerifications.GetByIdAsync(request.VerificationId, cancellationToken);
        if (verification == null)
        {
            throw new NotFoundException($"Không tìm thấy hồ sơ KYC với ID '{request.VerificationId}'.");
        }

        verification.VerificationStatus = request.Status;
        verification.RejectionReason = request.RejectionReason?.Trim();
        verification.VerifiedAt = DateTime.UtcNow;
        verification.VerifiedByUserId = request.AdminUserId;
        verification.VerificationMethod = "MANUAL_ADMIN_REVIEW";
        verification.UpdatedDate = DateTime.UtcNow;

        if (verification.UserProfile != null)
        {
            verification.UserProfile.VerificationStatus = request.Status;
            verification.UserProfile.UpdatedDate = DateTime.UtcNow;

            // Nếu Admin phê duyệt, tự động khởi tạo/kích hoạt cặp khóa ký số PKI cho người dùng
            if (request.Status == VerificationStatus.VERIFIED)
            {
                var user = await _unitOfWork.Users.GetByIdAsync(verification.UserProfile.UserAccountId, cancellationToken);
                if (user != null)
                {
                    if (user.UserSecurityProfile == null)
                    {
                        var keyPair = _pkiKeyService.GenerateKeyPair();
                        user.UserSecurityProfile = new UserSecurityProfile
                        {
                            UserAccountId = user.Id,
                            PublicKey = keyPair.PublicKeyPem,
                            EncryptedPrivateKey = keyPair.EncryptedPrivateKey,
                            KeyHash = keyPair.KeyHash,
                            KeyGeneratedAt = DateTime.UtcNow,
                            UserSecurityProfileStatus = UserSecurityProfileStatus.ACTIVE,
                            IsActive = true,
                            CreatedDate = DateTime.UtcNow
                        };
                    }
                    else
                    {
                        user.UserSecurityProfile.UserSecurityProfileStatus = UserSecurityProfileStatus.ACTIVE;
                        user.UserSecurityProfile.UpdatedDate = DateTime.UtcNow;
                    }

                    user.UpdatedDate = DateTime.UtcNow;
                    _unitOfWork.Users.Update(user);
                }
            }
        }

        _unitOfWork.IdentityVerifications.Update(verification);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
