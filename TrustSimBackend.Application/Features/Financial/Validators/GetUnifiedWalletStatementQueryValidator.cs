using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class GetUnifiedWalletStatementQueryValidator : AbstractValidator<GetUnifiedWalletStatementQuery>
{
    private static readonly HashSet<string> ValidSourceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "WALLET",
        "AUCTION",
        "AUCTION_DEPOSIT",
        "ESCROW",
        "COMPENSATION"
    };

    private static readonly HashSet<string> ValidDirections = new(StringComparer.OrdinalIgnoreCase)
    {
        "CREDIT",
        "DEBIT",
        "LOCK"
    };

    public GetUnifiedWalletStatementQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("ID người dùng không được để trống.");

        RuleFor(x => x.Filter.PageIndex)
            .GreaterThanOrEqualTo(1).WithMessage("Số trang (pageIndex) phải lớn hơn hoặc bằng 1.");

        RuleFor(x => x.Filter.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Kích thước trang (pageSize) phải từ 1 đến 100.");

        RuleFor(x => x.Filter)
            .Must(f => !f.FromDate.HasValue || !f.ToDate.HasValue || f.FromDate <= f.ToDate)
            .WithMessage("Ngày bắt đầu (fromDate) phải nhỏ hơn hoặc bằng ngày kết thúc (toDate).");

        RuleFor(x => x.Filter)
            .Must(f => !f.MinAmount.HasValue || !f.MaxAmount.HasValue || f.MinAmount <= f.MaxAmount)
            .WithMessage("Số tiền tối thiểu (minAmount) phải nhỏ hơn hoặc bằng số tiền tối đa (maxAmount).");

        RuleFor(x => x.Filter.SourceType)
            .Must(s => string.IsNullOrWhiteSpace(s) || ValidSourceTypes.Contains(s))
            .WithMessage("Nguồn biến động (sourceType) không hợp lệ. Chỉ chấp nhận: WALLET, AUCTION, AUCTION_DEPOSIT, ESCROW, COMPENSATION hoặc để trống.");

        RuleFor(x => x.Filter.Direction)
            .Must(d => string.IsNullOrWhiteSpace(d) || ValidDirections.Contains(d))
            .WithMessage("Hướng biến động (direction) không hợp lệ. Chỉ chấp nhận: CREDIT, DEBIT, LOCK hoặc để trống.");
    }
}
