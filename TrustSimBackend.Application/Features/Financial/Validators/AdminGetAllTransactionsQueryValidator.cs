using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class AdminGetAllTransactionsQueryValidator : AbstractValidator<AdminGetAllTransactionsQuery>
{
    public AdminGetAllTransactionsQueryValidator()
    {
        RuleFor(v => v.Filter.PageIndex)
            .GreaterThanOrEqualTo(1).WithMessage("Trang bắt đầu tối thiểu là 1.");

        RuleFor(v => v.Filter.PageSize)
            .InclusiveBetween(1, 200).WithMessage("Số lượng bản ghi mỗi trang phải từ 1 đến 200.");

        RuleFor(v => v.Filter.Type!.Value)
            .IsInEnum().WithMessage("Loại giao dịch không hợp lệ.")
            .When(v => v.Filter.Type.HasValue);

        RuleFor(v => v.Filter.Status!.Value)
            .IsInEnum().WithMessage("Trạng thái giao dịch không hợp lệ.")
            .When(v => v.Filter.Status.HasValue);
    }
}
