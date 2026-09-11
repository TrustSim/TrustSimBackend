using FluentValidation;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Sim.Validators;

public class DeleteCarrierCommandValidator : AbstractValidator<DeleteCarrierCommand>
{
    public DeleteCarrierCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty().WithMessage("Id nhà mạng không được để trống.");
    }
}
