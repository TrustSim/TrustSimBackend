using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class HoldEscrowDepositCommandValidator : AbstractValidator<HoldEscrowDepositCommand>
{
    public HoldEscrowDepositCommandValidator()
    {
        RuleFor(v => v.WinnerUserId)
            .NotEmpty().WithMessage("WinnerUserId không được để trống.");

        RuleFor(v => v.EscrowId)
            .NotEmpty().WithMessage("EscrowId không được để trống.");

        RuleFor(v => v.TotalEscrowAmount)
            .GreaterThan(0).WithMessage("Tổng số tiền ký quỹ Escrow phải lớn hơn 0 VNĐ.")
            .LessThanOrEqualTo(50_000_000_000).WithMessage("Tổng số tiền ký quỹ Escrow không vượt quá 50,000,000,000 VNĐ.")
            .Must(a => a == Math.Floor(a)).WithMessage("Tổng số tiền ký quỹ phải là số nguyên VNĐ.");

        RuleFor(v => v.ExistingDepositAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Số tiền cọc sẵn có không được âm.")
            .LessThanOrEqualTo(v => v.TotalEscrowAmount).WithMessage("Số tiền cọc sẵn có không được vượt quá tổng giá trị ký quỹ Escrow.")
            .Must(a => a == Math.Floor(a)).WithMessage("Số tiền cọc sẵn có phải là số nguyên VNĐ.");

        RuleFor(v => v.Description)
            .MaximumLength(500).WithMessage("Mô tả ký quỹ không vượt quá 500 ký tự.")
            .When(v => !string.IsNullOrEmpty(v.Description));
    }
}
