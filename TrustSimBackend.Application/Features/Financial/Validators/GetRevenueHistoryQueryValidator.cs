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

        RuleFor(x => x.Filter.TransactionType)
            .Must(t => !t.HasValue || (t.Value == Domain.Enums.LedgerTransactionType.ESCROW_RELEASE ||
                                       t.Value == Domain.Enums.LedgerTransactionType.AUCTION_DEPOSIT_FORFEITED ||
                                       t.Value == Domain.Enums.LedgerTransactionType.ADMIN_PROFIT_WITHDRAW))
            .WithMessage("Loại giao dịch không hợp lệ cho lịch sử doanh thu sàn. Chỉ chấp nhận ESCROW_RELEASE, AUCTION_DEPOSIT_FORFEITED hoặc ADMIN_PROFIT_WITHDRAW.");
    }
}
