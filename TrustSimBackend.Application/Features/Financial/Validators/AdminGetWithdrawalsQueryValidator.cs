using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class AdminGetWithdrawalsQueryValidator : AbstractValidator<AdminGetWithdrawalsQuery>
{
    public AdminGetWithdrawalsQueryValidator()
    {
        RuleFor(v => v.Filter.PageIndex)
            .GreaterThanOrEqualTo(1).WithMessage("Trang bắt đầu tối thiểu là 1.");

        RuleFor(v => v.Filter.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Số lượng bản ghi mỗi trang phải từ 1 đến 100.");

        RuleFor(v => v.Filter.Status!.Value)
            .IsInEnum().WithMessage("Trạng thái rút tiền không hợp lệ.")
            .When(v => v.Filter.Status.HasValue);
    }
}
