using FluentValidation;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Trade.Validators;

public class UpdateTradeStatusCommandValidator : AbstractValidator<UpdateTradeStatusCommand>
{
    public UpdateTradeStatusCommandValidator()
    {
        RuleFor(v => v.TradeId)
            .NotEmpty().WithMessage("TradeId không được để trống.");

        RuleFor(v => v.NewStatus)
            .IsInEnum().WithMessage("Trạng thái giao dịch không hợp lệ.");
    }
}
