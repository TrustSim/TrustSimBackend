using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class GetRevenueHistoryQueryValidator : AbstractValidator<GetRevenueHistoryQuery>
{
    public GetRevenueHistoryQueryValidator()
    {
        RuleFor(x => x.PageIndex)
            .GreaterThanOrEqualTo(1).WithMessage("Số trang (pageIndex) phải lớn hơn hoặc bằng 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Kích thước trang (pageSize) phải từ 1 đến 100.");

        RuleFor(x => x)
            .Must(x => !x.FromDate.HasValue || !x.ToDate.HasValue || x.FromDate <= x.ToDate)
            .WithMessage("Ngày bắt đầu (fromDate) phải nhỏ hơn hoặc bằng ngày kết thúc (toDate).");
    }
}
