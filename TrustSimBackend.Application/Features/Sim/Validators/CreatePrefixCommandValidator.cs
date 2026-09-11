using FluentValidation;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Sim.Validators;

public class CreatePrefixCommandValidator : AbstractValidator<CreatePrefixCommand>
{
    public CreatePrefixCommandValidator()
    {
        RuleFor(v => v.PrefixValue)
            .NotEmpty().WithMessage("Giá trị đầu số không được để trống.")
            .Matches(@"^(0|\+84)[0-9]{1,4}$").WithMessage("Đầu số không đúng định dạng (ví dụ: 039, 090, +849).")
            .MaximumLength(10).WithMessage("Đầu số không vượt quá 10 ký tự.");

        RuleFor(v => v.SimCarrierId)
            .NotEmpty().WithMessage("SimCarrierId không được để trống.");
    }
}
