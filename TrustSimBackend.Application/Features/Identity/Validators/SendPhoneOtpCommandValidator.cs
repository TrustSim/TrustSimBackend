using FluentValidation;
using TrustSimBackend.Application.Features.Identity.Commands;

namespace TrustSimBackend.Application.Features.Identity.Validators;

/// <summary>
/// Validator kiểm tra tính hợp lệ của số điện thoại trước khi gửi SMS OTP.
/// </summary>
public class SendPhoneOtpCommandValidator : AbstractValidator<SendPhoneOtpCommand>
{
    public SendPhoneOtpCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Matches(@"^(0|\+84|84)(3|5|7|8|9)[0-9]{8}$")
            .WithMessage("Số điện thoại không đúng định dạng số di động Việt Nam (Ví dụ: 0869192776, +84869192776).");

        RuleFor(x => x.Purpose)
            .IsInEnum().WithMessage("Mục đích xác thực OTP không hợp lệ.");
    }
}
