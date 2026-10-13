using FluentValidation;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Trade.Validators;

/// <summary>
/// Trình kiểm định hợp lệ cho lệnh gửi OTP ký hợp đồng.
/// </summary>
public class SendContractSigningOtpCommandValidator : AbstractValidator<SendContractSigningOtpCommand>
{
    public SendContractSigningOtpCommandValidator()
    {
        RuleFor(x => x.TradeId)
            .NotEmpty().WithMessage("TradeId không được để trống.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId không được để trống.");
    }
}
