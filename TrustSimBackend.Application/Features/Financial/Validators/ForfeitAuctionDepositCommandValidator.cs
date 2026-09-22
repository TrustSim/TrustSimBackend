using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class ForfeitAuctionDepositCommandValidator : AbstractValidator<ForfeitAuctionDepositCommand>
{
    public ForfeitAuctionDepositCommandValidator()
    {
        RuleFor(v => v.UserId)
            .NotEmpty().WithMessage("UserId không được để trống.");

        RuleFor(v => v.AuctionId)
            .NotEmpty().WithMessage("AuctionId không được để trống.");

        RuleFor(v => v.AmountToForfeit)
            .GreaterThan(0).WithMessage("Số tiền tịch thu cọc phải lớn hơn 0 VNĐ.")
            .LessThanOrEqualTo(10_000_000_000).WithMessage("Số tiền tịch thu cọc không vượt quá 10,000,000,000 VNĐ.")
            .Must(a => a == Math.Floor(a)).WithMessage("Số tiền tịch thu cọc phải là số nguyên VNĐ.");

        RuleFor(v => v.ViolationReason)
            .NotEmpty().WithMessage("Lý do tịch thu cọc vi phạm không được để trống.")
            .MinimumLength(5).WithMessage("Lý do tịch thu cọc phải có ít nhất 5 ký tự.")
            .MaximumLength(500).WithMessage("Lý do tịch thu cọc không vượt quá 500 ký tự.");
    }
}
