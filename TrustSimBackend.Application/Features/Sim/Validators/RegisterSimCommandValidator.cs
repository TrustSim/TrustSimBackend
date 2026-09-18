using FluentValidation;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Sim.Validators;

public class RegisterSimCommandValidator : AbstractValidator<RegisterSimCommand>
{
    public RegisterSimCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Matches(@"^0\d{9}$").WithMessage("Số điện thoại phải bao gồm đúng 10 chữ số và bắt đầu bằng số 0 (VD: 0909123456).");

        RuleFor(x => x.FormatRhythmId)
            .NotEmpty().WithMessage("Vui lòng chọn kiểu nhịp định dạng số SIM.");
    }
}
