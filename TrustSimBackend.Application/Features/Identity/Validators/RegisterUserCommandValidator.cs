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
            .Matches("^[a-zA-Z0-9_.]+$").WithMessage("Tên đăng nhập chỉ chứa chữ cái, chữ số, dấu gạch dưới và dấu chấm.");

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

        RuleFor(v => v.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MaximumLength(150).WithMessage("Họ và tên không vượt quá 150 ký tự.");

        RuleFor(v => v.PhoneNumber)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Matches(@"^(0|\+84)(3|5|7|8|9)[0-9]{8}$").WithMessage("Số điện thoại không đúng định dạng di động Việt Nam (VD: 0981234567).");

        RuleFor(v => v.IdentityNumber)
            .NotEmpty().WithMessage("Số CCCD/CMND không được để trống.")
            .Matches(@"^([0-9]{9}|[0-9]{12})$").WithMessage("Số CCCD/CMND phải gồm chính xác 9 hoặc 12 chữ số.");
    }
}
