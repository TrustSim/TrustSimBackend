using FluentValidation;

namespace TrustSimBackend.Application.Features.Financial.Queries.GetWalletTransactions;

/// <summary>
/// Validator kiểm tra tham số phân trang của truy vấn lịch sử giao dịch.
/// </summary>
public class GetWalletTransactionsQueryValidator : AbstractValidator<GetWalletTransactionsQuery>
{
    public GetWalletTransactionsQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("ID người dùng không được để trống.");

        RuleFor(x => x.PageIndex)
            .GreaterThanOrEqualTo(1).WithMessage("Trang bắt đầu tối thiểu là 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Số lượng phần tử mỗi trang từ 1 đến 100.");
    }
}
