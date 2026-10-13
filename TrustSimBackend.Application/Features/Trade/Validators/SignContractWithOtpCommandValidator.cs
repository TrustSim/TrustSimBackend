using FluentValidation;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Trade.Validators;

/// <summary>
/// Trình kiểm định hợp lệ cho lệnh ký hợp đồng bằng mã OTP.
/// </summary>
public class SignContractWithOtpCommandValidator : AbstractValidator<SignContractWithOtpCommand>
{
    public SignContractWithOtpCommandValidator()
    {
        RuleFor(x => x.TradeId)
            .NotEmpty().WithMessage("TradeId không được để trống.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId không được để trống.");

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("Mã OTP không được để trống.")
            .Length(6).WithMessage("Mã OTP phải có đúng 6 chữ số.")
            .Matches(@"^\d{6}$").WithMessage("Mã OTP chỉ được chứa ký tự số.");
    }
}
