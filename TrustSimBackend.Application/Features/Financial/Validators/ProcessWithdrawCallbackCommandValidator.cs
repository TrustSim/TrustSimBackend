using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class ProcessWithdrawCallbackCommandValidator : AbstractValidator<ProcessWithdrawCallbackCommand>
{
    public ProcessWithdrawCallbackCommandValidator()
    {
        RuleFor(v => v.CallbackData)
            .NotNull().WithMessage("Dữ liệu callback không được để trống.");

        When(v => v.CallbackData != null, () =>
        {
            RuleFor(v => v.CallbackData.TransactionCode)
                .NotEmpty().WithMessage("Mã giao dịch không được để trống.")
                .Matches(@"^[A-Za-z0-9_-]{4,50}$").WithMessage("Mã giao dịch không hợp lệ.");

            RuleFor(v => v.CallbackData.FailureReason)
                .NotEmpty().WithMessage("Lý do chi trả thất bại bắt buộc phải có khi giao dịch thất bại.")
                .When(v => !v.CallbackData.IsSuccess);

            RuleFor(v => v.CallbackData.FailureReason)
                .MaximumLength(500).WithMessage("Lý do thất bại không vượt quá 500 ký tự.")
                .When(v => !string.IsNullOrEmpty(v.CallbackData.FailureReason));
        });
    }
}
