using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class ApproveWithdrawCommandValidator : AbstractValidator<ApproveWithdrawCommand>
{
    public ApproveWithdrawCommandValidator()
    {
        RuleFor(v => v.TransactionId)
            .NotEmpty().WithMessage("TransactionId không được để trống.");

        RuleFor(v => v.AdminId)
            .NotEmpty().WithMessage("AdminId không được để trống.");

        RuleFor(v => v.Note)
            .MaximumLength(500).WithMessage("Ghi chú không vượt quá 500 ký tự.")
            .When(v => !string.IsNullOrEmpty(v.Note));
    }
}

