using FluentValidation;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Identity.Validators;

public class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(v => v.UserNameOrEmail)
            .NotEmpty().WithMessage("Tên đăng nhập hoặc Email không được để trống.");

        RuleFor(v => v.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.");
    }
}
