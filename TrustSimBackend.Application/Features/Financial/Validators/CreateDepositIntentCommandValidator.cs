using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class CreateDepositIntentCommandValidator : AbstractValidator<CreateDepositIntentCommand>
{
    public CreateDepositIntentCommandValidator()
    {
        RuleFor(v => v.UserId)
            .NotEmpty().WithMessage("UserId không được để trống.");

        RuleFor(v => v.Amount)
            .GreaterThanOrEqualTo(10_000).WithMessage("Số tiền nạp tối thiểu là 10,000 VNĐ.")
            .LessThanOrEqualTo(500_000_000).WithMessage("Số tiền nạp tối đa mỗi lần là 500,000,000 VNĐ.")
            .Must(a => a == Math.Floor(a)).WithMessage("Số tiền nạp phải là số nguyên VNĐ.");

        RuleFor(v => v.IdempotencyKey)
            .MaximumLength(128).WithMessage("IdempotencyKey không vượt quá 128 ký tự.")
            .When(v => !string.IsNullOrEmpty(v.IdempotencyKey));
    }
}
