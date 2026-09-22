using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class ProcessWithdrawFailedCommandValidator : AbstractValidator<ProcessWithdrawFailedCommand>
{
    public ProcessWithdrawFailedCommandValidator()
    {
        RuleFor(v => v.TransactionCode)
            .NotEmpty().WithMessage("Mã giao dịch không được để trống.")
            .Matches(@"^[A-Za-z0-9_-]{4,50}$").WithMessage("Mã giao dịch không hợp lệ.");

        RuleFor(v => v.FailureReason)
            .NotEmpty().WithMessage("Lý do chi trả thất bại không được để trống.")
            .MaximumLength(500).WithMessage("Lý do chi trả thất bại không vượt quá 500 ký tự.");
    }
}
