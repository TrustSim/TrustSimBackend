using FluentValidation;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Sim.Validators;

public class DeleteCategoryCommandValidator : AbstractValidator<DeleteCategoryCommand>
{
    public DeleteCategoryCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty().WithMessage("Id danh mục không được để trống.");
    }
}
