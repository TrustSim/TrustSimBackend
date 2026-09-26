using FluentValidation;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Auction.Validators;

public class ConfirmWinnerPaymentCommandValidator : AbstractValidator<ConfirmWinnerPaymentCommand>
{
    public ConfirmWinnerPaymentCommandValidator()
    {
        RuleFor(v => v.AuctionId)
            .NotEmpty().WithMessage("AuctionId không được để trống.");
    }
}
