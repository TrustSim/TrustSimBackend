using FluentValidation;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Identity.Validators;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(v => v.UserId)
            .NotEmpty().WithMessage("UserId không được để trống.");

        RuleFor(v => v.CurrentPassword)
            .NotEmpty().WithMessage("Mật khẩu hiện tại không được để trống.");

        RuleFor(v => v.NewPassword)
            .NotEmpty().WithMessage("Mật khẩu mới không được để trống.")
            .MinimumLength(8).WithMessage("Mật khẩu mới phải có ít nhất 8 ký tự.")
            .Matches(@"[A-Z]").WithMessage("Mật khẩu mới phải chứa ít nhất 1 chữ hoa.")
            .Matches(@"[a-z]").WithMessage("Mật khẩu mới phải chứa ít nhất 1 chữ thường.")
            .Matches(@"[0-9]").WithMessage("Mật khẩu mới phải chứa ít nhất 1 chữ số.")
            .Matches(@"[\!\?\*\@\#\$\%\^\&\+\=]").WithMessage("Mật khẩu mới phải chứa ít nhất 1 ký tự đặc biệt.")
            .NotEqual(v => v.CurrentPassword).WithMessage("Mật khẩu mới không được trùng với mật khẩu cũ.");

        RuleFor(v => v.ConfirmNewPassword)
            .Equal(v => v.NewPassword).WithMessage("Xác nhận mật khẩu mới không khớp.");
    }
}
