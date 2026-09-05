using FluentValidation;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Identity.Validators;

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(v => v.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Định dạng Email không hợp lệ.");

        RuleFor(v => v.ResetCode)
            .NotEmpty().WithMessage("Mã xác thực không được để trống.")
            .Length(6).WithMessage("Mã xác thực phải gồm 6 chữ số.");

        RuleFor(v => v.NewPassword)
            .NotEmpty().WithMessage("Mật khẩu mới không được để trống.")
            .MinimumLength(8).WithMessage("Mật khẩu mới phải có ít nhất 8 ký tự.")
            .Matches(@"[A-Z]").WithMessage("Mật khẩu mới phải chứa ít nhất 1 chữ hoa.")
            .Matches(@"[a-z]").WithMessage("Mật khẩu mới phải chứa ít nhất 1 chữ thường.")
            .Matches(@"[0-9]").WithMessage("Mật khẩu mới phải chứa ít nhất 1 chữ số.")
            .Matches(@"[\!\?\*\@\#\$\%\^\&\+\=]").WithMessage("Mật khẩu mới phải chứa ít nhất 1 ký tự đặc biệt.");

        RuleFor(v => v.ConfirmNewPassword)
            .Equal(v => v.NewPassword).WithMessage("Xác nhận mật khẩu mới không khớp.");
    }
}
