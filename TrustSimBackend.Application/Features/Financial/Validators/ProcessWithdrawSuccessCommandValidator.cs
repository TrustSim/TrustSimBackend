using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class ProcessWithdrawSuccessCommandValidator : AbstractValidator<ProcessWithdrawSuccessCommand>
{
    public ProcessWithdrawSuccessCommandValidator()
    {
        RuleFor(v => v.TransactionCode)
            .NotEmpty().WithMessage("Mã giao dịch không được để trống.")
            .Matches(@"^[A-Za-z0-9_-]{4,50}$").WithMessage("Mã giao dịch không hợp lệ (phải từ 4 đến 50 ký tự chữ, số hoặc gạch nối).");

        RuleFor(v => v.ExternalBankRef)
            .MaximumLength(100).WithMessage("Mã tham chiếu ngân hàng không vượt quá 100 ký tự.")
            .When(v => !string.IsNullOrEmpty(v.ExternalBankRef));
    }
}
