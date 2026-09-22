using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class AdminGetWithdrawalsQueryValidator : AbstractValidator<AdminGetWithdrawalsQuery>
{
    public AdminGetWithdrawalsQueryValidator()
    {
        RuleFor(v => v.PageIndex)
            .GreaterThanOrEqualTo(1).WithMessage("Trang bắt đầu tối thiểu là 1.");

        RuleFor(v => v.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Số lượng bản ghi mỗi trang phải từ 1 đến 100.");

        RuleFor(v => v.Status!.Value)
            .IsInEnum().WithMessage("Trạng thái rút tiền không hợp lệ.")
            .When(v => v.Status.HasValue);
    }
}
