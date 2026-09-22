using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class AdminAdjustBalanceCommandValidator : AbstractValidator<AdminAdjustBalanceCommand>
{
    public AdminAdjustBalanceCommandValidator()
    {
        RuleFor(v => v.WalletId)
            .NotEmpty().WithMessage("WalletId không được để trống.");

        RuleFor(v => v.AdminId)
            .NotEmpty().WithMessage("AdminId không được để trống.");

        RuleFor(v => v.Amount)
            .Must(a => a != 0).WithMessage("Số tiền điều chỉnh phải khác 0 VNĐ.")
            .Must(a => Math.Abs(a) <= 1_000_000_000).WithMessage("Số tiền điều chỉnh không được vượt quá 1,000,000,000 VNĐ.")
            .Must(a => a == Math.Floor(a)).WithMessage("Số tiền điều chỉnh phải là số nguyên VNĐ.");

        RuleFor(v => v.Reason)
            .NotEmpty().WithMessage("Lý do điều chỉnh số dư không được để trống.")
            .MinimumLength(5).WithMessage("Lý do điều chỉnh số dư phải có ít nhất 5 ký tự.")
            .MaximumLength(500).WithMessage("Lý do điều chỉnh số dư không vượt quá 500 ký tự.");
    }
}
