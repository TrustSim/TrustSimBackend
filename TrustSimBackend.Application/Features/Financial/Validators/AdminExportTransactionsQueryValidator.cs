using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class AdminExportTransactionsQueryValidator : AbstractValidator<AdminExportTransactionsQuery>
{
    public AdminExportTransactionsQueryValidator()
    {
        RuleFor(v => v.Type!.Value)
            .IsInEnum().WithMessage("Loại giao dịch không hợp lệ.")
            .When(v => v.Type.HasValue);

        RuleFor(v => v.Status!.Value)
            .IsInEnum().WithMessage("Trạng thái giao dịch không hợp lệ.")
            .When(v => v.Status.HasValue);
    }
}
