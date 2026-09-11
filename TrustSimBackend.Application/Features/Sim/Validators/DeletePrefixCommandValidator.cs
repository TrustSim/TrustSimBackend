using FluentValidation;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Sim.Validators;

public class DeletePrefixCommandValidator : AbstractValidator<DeletePrefixCommand>
{
    public DeletePrefixCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty().WithMessage("Id đầu số không được để trống.");
    }
}
