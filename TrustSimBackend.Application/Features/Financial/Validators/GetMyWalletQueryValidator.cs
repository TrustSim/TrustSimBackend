using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class GetMyWalletQueryValidator : AbstractValidator<GetMyWalletQuery>
{
    public GetMyWalletQueryValidator()
    {
        RuleFor(v => v.UserId)
            .NotEmpty().WithMessage("UserId không được để trống.");
    }
}
