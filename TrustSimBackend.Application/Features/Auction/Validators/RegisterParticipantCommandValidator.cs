using FluentValidation;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Auction.Validators;

public class RegisterParticipantCommandValidator : AbstractValidator<RegisterParticipantCommand>
{
    public RegisterParticipantCommandValidator()
    {
        RuleFor(v => v.AuctionId)
            .NotEmpty().WithMessage("AuctionId không được để trống.");

        RuleFor(v => v.UserId)
            .NotEmpty().WithMessage("UserId không được để trống.");
    }
}
