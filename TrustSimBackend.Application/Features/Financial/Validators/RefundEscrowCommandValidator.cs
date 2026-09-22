using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class RefundEscrowCommandValidator : AbstractValidator<RefundEscrowCommand>
{
    public RefundEscrowCommandValidator()
    {
        RuleFor(v => v.BuyerUserId)
            .NotEmpty().WithMessage("BuyerUserId không được để trống.");

        RuleFor(v => v.EscrowId)
            .NotEmpty().WithMessage("EscrowId không được để trống.");

        RuleFor(v => v.TotalEscrowAmount)
            .GreaterThan(0).WithMessage("Tổng số tiền hoàn trả Escrow phải lớn hơn 0 VNĐ.")
            .LessThanOrEqualTo(50_000_000_000).WithMessage("Tổng số tiền hoàn trả không vượt quá 50,000,000,000 VNĐ.")
            .Must(a => a == Math.Floor(a)).WithMessage("Tổng số tiền hoàn trả phải là số nguyên VNĐ.");

        RuleFor(v => v.RefundReason)
            .NotEmpty().WithMessage("Lý do hoàn trả tiền ký quỹ Escrow không được để trống.")
            .MinimumLength(5).WithMessage("Lý do hoàn trả phải có ít nhất 5 ký tự.")
            .MaximumLength(500).WithMessage("Lý do hoàn trả không vượt quá 500 ký tự.");
    }
}
