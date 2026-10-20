using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class AdminExportTransactionsQueryValidator : AbstractValidator<AdminExportTransactionsQuery>
{
    public AdminExportTransactionsQueryValidator()
    {
        RuleFor(v => v.Filter.Type!.Value)
            .IsInEnum().WithMessage("Loại giao dịch không hợp lệ.")
            .When(v => v.Filter != null && v.Filter.Type.HasValue);

        RuleFor(v => v.Filter.Status!.Value)
            .IsInEnum().WithMessage("Trạng thái giao dịch không hợp lệ.")
            .When(v => v.Filter != null && v.Filter.Status.HasValue);
    }
}
