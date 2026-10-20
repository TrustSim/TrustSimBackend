using FluentValidation;
using TrustSimBackend.Application.Features.Financial.Queries;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class GetLedgerJournalsQueryValidator : AbstractValidator<GetLedgerJournalsQuery>
{
    public GetLedgerJournalsQueryValidator()
    {
        RuleFor(v => v.Filter.PageIndex)
            .GreaterThanOrEqualTo(1).WithMessage("Trang bắt đầu tối thiểu là 1.");

        RuleFor(v => v.Filter.PageSize)
            .InclusiveBetween(1, 200).WithMessage("Số lượng bản ghi mỗi trang phải từ 1 đến 200.");

        RuleFor(v => v.Filter.TransactionType!.Value)
            .IsInEnum().WithMessage("Loại chứng từ kế toán không hợp lệ.")
            .When(v => v.Filter.TransactionType.HasValue);

        RuleFor(v => v.Filter)
            .Must(f => !f.FromDate.HasValue || !f.ToDate.HasValue || f.FromDate.Value <= f.ToDate.Value)
            .WithMessage("Từ ngày (FromDate) phải nhỏ hơn hoặc bằng Đến ngày (ToDate).");
    }
}
