using FluentValidation;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Identity.Validators;

public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(v => v.UserName)
            .NotEmpty().WithMessage("Tên đăng nhập không được để trống.")
            .MinimumLength(3).WithMessage("Tên đăng nhập phải có ít nhất 3 ký tự.")
            .MaximumLength(50).WithMessage("Tên đăng nhập không vượt quá 50 ký tự.")
            .Matches(@"^[\p{L}0-9_.\s]+$").WithMessage("Tên đăng nhập chỉ chứa chữ cái, chữ số, dấu gạch dưới, dấu chấm và khoảng trắng.")
            .Must(name => string.IsNullOrWhiteSpace(name) || (!name.StartsWith(' ') && !name.EndsWith(' '))).WithMessage("Tên đăng nhập không được bắt đầu hoặc kết thúc bằng khoảng trắng.");

        RuleFor(v => v.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Định dạng Email không hợp lệ.")
            .MaximumLength(255).WithMessage("Email không vượt quá 255 ký tự.");

        RuleFor(v => v.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(8).WithMessage("Mật khẩu phải có ít nhất 8 ký tự.")
            .Matches(@"[A-Z]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ hoa.")
            .Matches(@"[a-z]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ thường.")
            .Matches(@"[0-9]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ số.")
            .Matches(@"[\!\?\*\@\#\$\%\^\&\+\=]").WithMessage("Mật khẩu phải chứa ít nhất 1 ký tự đặc biệt (!?*@#$%^&+=).");
    }
}
