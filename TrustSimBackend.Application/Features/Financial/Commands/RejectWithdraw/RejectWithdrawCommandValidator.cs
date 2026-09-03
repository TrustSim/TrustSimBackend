using FluentValidation;

namespace TrustSimBackend.Application.Features.Financial.Commands.RejectWithdraw;

/// <summary>
/// Validator kiểm tra lý do từ chối rút tiền của Admin.
/// </summary>
public class RejectWithdrawCommandValidator : AbstractValidator<RejectWithdrawCommand>
{
    public RejectWithdrawCommandValidator()
    {
        RuleFor(x => x.TransactionId)
            .NotEmpty().WithMessage("ID giao dịch không được để trống.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do từ chối không được để trống.")
            .MinimumLength(5).WithMessage("Lý do từ chối phải có ít nhất 5 ký tự.")
            .MaximumLength(500).WithMessage("Lý do từ chối tối đa 500 ký tự.");
    }
}
