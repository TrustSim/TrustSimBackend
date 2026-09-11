using FluentValidation;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Sim.Validators;

public class CreateCarrierCommandValidator : AbstractValidator<CreateCarrierCommand>
{
    public CreateCarrierCommandValidator()
    {
        RuleFor(v => v.CarrierName)
            .NotEmpty().WithMessage("Tên nhà mạng không được để trống.")
            .MaximumLength(100).WithMessage("Tên nhà mạng không vượt quá 100 ký tự.");

        RuleFor(v => v.CarrierCode)
            .NotEmpty().WithMessage("Mã nhà mạng không được để trống.")
            .MaximumLength(20).WithMessage("Mã nhà mạng không vượt quá 20 ký tự.")
            .Matches(@"^[A-Z0-9_]+$").WithMessage("Mã nhà mạng chỉ chứa chữ hoa, chữ số và dấu gạch dưới.");

        RuleFor(v => v.LogoImg)
            .MaximumLength(500).WithMessage("Đường dẫn logo không vượt quá 500 ký tự.")
            .When(v => !string.IsNullOrWhiteSpace(v.LogoImg));
    }
}
