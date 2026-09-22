using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class VerifyLedgerChainCommandValidator : AbstractValidator<VerifyLedgerChainCommand>
{
    public VerifyLedgerChainCommandValidator()
    {
        RuleFor(v => v.FromSequence)
            .GreaterThanOrEqualTo(1).WithMessage("Sequence bắt đầu kiểm tra phải từ 1.");

        RuleFor(v => v.Limit)
            .InclusiveBetween(1, 50_000).WithMessage("Số lượng bản ghi kiểm tra mỗi lần phải từ 1 đến 50,000.");
    }
}
