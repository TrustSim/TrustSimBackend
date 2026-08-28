using FluentValidation;

namespace TrustSimBackend.Application.Features.Identity.Commands.ForgotPassword;

/// <summary>
/// Validator kiểm tra tính hợp lệ email khi yêu cầu quên mật khẩu.
/// </summary>
public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Định dạng email không hợp lệ.");
    }
}
