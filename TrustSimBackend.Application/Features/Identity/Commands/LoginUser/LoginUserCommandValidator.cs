using FluentValidation;

namespace TrustSimBackend.Application.Features.Identity.Commands.LoginUser;

/// <summary>
/// Validator kiểm tra tính hợp lệ dữ liệu đầu vào khi Đăng nhập.
/// </summary>
public class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(x => x.UserNameOrEmail)
            .NotEmpty().WithMessage("Tên đăng nhập hoặc Email không được để trống.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.");
    }
}
