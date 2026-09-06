using FluentValidation;
using TrustSimBackend.Application.Features.Identity.Commands;

namespace TrustSimBackend.Application.Features.Identity.Validators;

/// <summary>
/// Validator kiểm tra tính hợp lệ của dữ liệu xác thực OTP.
/// </summary>
public class VerifyPhoneOtpCommandValidator : AbstractValidator<VerifyPhoneOtpCommand>
{
    public VerifyPhoneOtpCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Matches(@"^(0|\+84|84)(3|5|7|8|9)[0-9]{8}$")
            .WithMessage("Số điện thoại không đúng định dạng số di động Việt Nam.");

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("Mã OTP không được để trống.")
            .Length(6).WithMessage("Mã OTP phải có đúng 6 chữ số.")
            .Matches(@"^[0-9]{6}$").WithMessage("Mã OTP chỉ được chứa các ký tự số.");

        RuleFor(x => x.Purpose)
            .IsInEnum().WithMessage("Mục đích xác thực OTP không hợp lệ.");
    }
}
