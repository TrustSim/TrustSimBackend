using FluentValidation;
using TrustSimBackend.Application.Features.Financial.Queries;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class GetLedgerJournalsQueryValidator : AbstractValidator<GetLedgerJournalsQuery>
{
    public GetLedgerJournalsQueryValidator()
    {
        RuleFor(v => v.PageIndex)
            .GreaterThanOrEqualTo(1).WithMessage("Trang bắt đầu tối thiểu là 1.");

        RuleFor(v => v.PageSize)
            .InclusiveBetween(1, 200).WithMessage("Số lượng bản ghi mỗi trang phải từ 1 đến 200.");

        RuleFor(v => v.TransactionType!.Value)
            .IsInEnum().WithMessage("Loại chứng từ kế toán không hợp lệ.")
            .When(v => v.TransactionType.HasValue);

        RuleFor(v => v)
            .Must(v => !v.FromDate.HasValue || !v.ToDate.HasValue || v.FromDate.Value <= v.ToDate.Value)
            .WithMessage("Từ ngày (FromDate) phải nhỏ hơn hoặc bằng Đến ngày (ToDate).");
    }
}
