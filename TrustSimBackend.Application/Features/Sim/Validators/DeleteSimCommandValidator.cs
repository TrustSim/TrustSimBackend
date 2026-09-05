using FluentValidation;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Sim.Validators;

public class DeleteSimCommandValidator : AbstractValidator<DeleteSimCommand>
{
    public DeleteSimCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty().WithMessage("Id SIM không được để trống.");
    }
}
