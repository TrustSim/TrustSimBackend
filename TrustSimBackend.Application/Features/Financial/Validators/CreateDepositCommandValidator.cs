using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class CreateDepositCommandValidator : AbstractValidator<CreateDepositCommand>
{
    public CreateDepositCommandValidator()
    {
        RuleFor(v => v.UserId)
            .NotEmpty().WithMessage("UserId không được để trống.");

        RuleFor(v => v.Amount)
            .GreaterThanOrEqualTo(10_000).WithMessage("Số tiền nạp tối thiểu là 10,000 VNĐ.")
            .LessThanOrEqualTo(500_000_000).WithMessage("Số tiền nạp tối đa mỗi lần là 500,000,000 VNĐ.")
            .Must(a => a == Math.Floor(a)).WithMessage("Số tiền nạp phải là số nguyên VNĐ (không có phần thập phân).");

        RuleFor(v => v.Note)
            .MaximumLength(255).WithMessage("Ghi chú không được vượt quá 255 ký tự.")
            .When(v => !string.IsNullOrEmpty(v.Note));
    }
}

