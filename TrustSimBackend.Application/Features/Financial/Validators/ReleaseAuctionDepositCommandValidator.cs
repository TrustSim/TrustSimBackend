using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class ReleaseAuctionDepositCommandValidator : AbstractValidator<ReleaseAuctionDepositCommand>
{
    public ReleaseAuctionDepositCommandValidator()
    {
        RuleFor(v => v.UserId)
            .NotEmpty().WithMessage("UserId không được để trống.");

        RuleFor(v => v.AuctionId)
            .NotEmpty().WithMessage("AuctionId không được để trống.");

        RuleFor(v => v.AmountToRelease)
            .GreaterThan(0).WithMessage("Số tiền hoàn cọc phải lớn hơn 0 VNĐ.")
            .LessThanOrEqualTo(10_000_000_000).WithMessage("Số tiền hoàn cọc không vượt quá 10,000,000,000 VNĐ.")
            .Must(a => a == Math.Floor(a)).WithMessage("Số tiền hoàn cọc phải là số nguyên VNĐ.");

        RuleFor(v => v.Reason)
            .MaximumLength(500).WithMessage("Lý do hoàn cọc không vượt quá 500 ký tự.")
            .When(v => !string.IsNullOrEmpty(v.Reason));
    }
}
