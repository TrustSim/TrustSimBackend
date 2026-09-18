using FluentValidation;
using TrustSimBackend.Application.Features.Ai.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Ai.Validators;

public class ApplyValuationPriceCommandValidator : AbstractValidator<ApplyValuationPriceCommand>
{
    public ApplyValuationPriceCommandValidator()
    {
        RuleFor(x => x.ValuationId)
            .NotEmpty().WithMessage("ValuationId không được để trống.");
    }
}
