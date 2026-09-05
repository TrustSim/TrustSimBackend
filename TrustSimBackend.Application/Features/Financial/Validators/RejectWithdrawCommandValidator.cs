using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class RejectWithdrawCommandValidator : AbstractValidator<RejectWithdrawCommand>
{
    public RejectWithdrawCommandValidator()
    {
        RuleFor(v => v.TransactionId)
            .NotEmpty().WithMessage("TransactionId không được để trống.");

        RuleFor(v => v.AdminId)
            .NotEmpty().WithMessage("AdminId không được để trống.");

        RuleFor(v => v.Reason)
            .NotEmpty().WithMessage("Lý do từ chối không được để trống.")
            .MaximumLength(500).WithMessage("Lý do từ chối không vượt quá 500 ký tự.");
    }
}
