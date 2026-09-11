using FluentValidation;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Identity.Validators;

public class ReviewKycCommandValidator : AbstractValidator<ReviewKycCommand>
{
    public ReviewKycCommandValidator()
    {
        RuleFor(v => v.VerificationId)
            .NotEmpty().WithMessage("VerificationId không được để trống.");

        RuleFor(v => v.Status)
            .IsInEnum().WithMessage("Trạng thái xác minh không hợp lệ.")
            .Must(s => s == VerificationStatus.VERIFIED || s == VerificationStatus.REJECTED)
            .WithMessage("Trạng thái xét duyệt phải là VERIFIED hoặc REJECTED.");

        RuleFor(v => v.RejectionReason)
            .NotEmpty().WithMessage("Lý do từ chối không được để trống khi trạng thái là REJECTED.")
            .MaximumLength(1000).WithMessage("Lý do từ chối không vượt quá 1000 ký tự.")
            .When(v => v.Status == VerificationStatus.REJECTED);
    }
}
