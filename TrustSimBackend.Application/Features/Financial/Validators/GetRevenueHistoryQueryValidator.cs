using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class GetRevenueHistoryQueryValidator : AbstractValidator<GetRevenueHistoryQuery>
{
    public GetRevenueHistoryQueryValidator()
    {
        RuleFor(x => x.Filter.PageIndex)
            .GreaterThanOrEqualTo(1).WithMessage("Số trang (pageIndex) phải lớn hơn hoặc bằng 1.");

        RuleFor(x => x.Filter.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Kích thước trang (pageSize) phải từ 1 đến 100.");

        RuleFor(x => x.Filter)
            .Must(f => !f.FromDate.HasValue || !f.ToDate.HasValue || f.FromDate <= f.ToDate)
            .WithMessage("Ngày bắt đầu (fromDate) phải nhỏ hơn hoặc bằng ngày kết thúc (toDate).");
    }
}
