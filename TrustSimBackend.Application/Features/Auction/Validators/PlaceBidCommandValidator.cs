using FluentValidation;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Auction.Validators;

public class PlaceBidCommandValidator : AbstractValidator<PlaceBidCommand>
{
    public PlaceBidCommandValidator()
    {
        RuleFor(v => v.AuctionId)
            .NotEmpty().WithMessage("AuctionId không được để trống.");

        RuleFor(v => v.UserId)
            .NotEmpty().WithMessage("UserId không được để trống.");

        RuleFor(v => v.BidAmount)
            .GreaterThan(0).WithMessage("Số tiền đặt giá phải lớn hơn 0.");
    }
}
