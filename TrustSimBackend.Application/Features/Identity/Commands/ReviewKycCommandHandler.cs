using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Handler xử lý phê duyệt hoặc từ chối hồ sơ KYC.
/// </summary>
public class ReviewKycCommandHandler : IRequestHandler<ReviewKycCommand, bool>
{
    private readonly IIdentityVerificationRepository _kycRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReviewKycCommandHandler(
        IIdentityVerificationRepository kycRepository,
        IUnitOfWork unitOfWork)
    {
        _kycRepository = kycRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(ReviewKycCommand request, CancellationToken cancellationToken)
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

        return true;
    }
}
