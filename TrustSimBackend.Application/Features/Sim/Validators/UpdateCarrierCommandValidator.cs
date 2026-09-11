using FluentValidation;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Sim.Validators;

public class UpdateCarrierCommandValidator : AbstractValidator<UpdateCarrierCommand>
{
    public UpdateCarrierCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty().WithMessage("Id nhà mạng không được để trống.");

        RuleFor(v => v.CarrierName)
            .NotEmpty().WithMessage("Tên nhà mạng không được để trống.")
            .MaximumLength(100).WithMessage("Tên nhà mạng không vượt quá 100 ký tự.");

        RuleFor(v => v.LogoImg)
            .MaximumLength(500).WithMessage("Đường dẫn logo không vượt quá 500 ký tự.")
            .When(v => !string.IsNullOrWhiteSpace(v.LogoImg));
    }
}
