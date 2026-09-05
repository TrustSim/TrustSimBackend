using FluentValidation;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Trade.Validators;

public class CreateTradeTransactionCommandValidator : AbstractValidator<CreateTradeTransactionCommand>
{
    public CreateTradeTransactionCommandValidator()
    {
        RuleFor(v => v.BuyerId)
            .NotEmpty().WithMessage("BuyerId không được để trống.");

        RuleFor(v => v.SellerId)
            .NotEmpty().WithMessage("SellerId không được để trống.");

        RuleFor(v => v.AuctionId)
            .NotEmpty().WithMessage("AuctionId không được để trống.");

        RuleFor(v => v.SimNumberId)
            .NotEmpty().WithMessage("SimNumberId không được để trống.");

        RuleFor(v => v.EscrowAmount)
            .GreaterThan(0).WithMessage("Số tiền ký quỹ phải lớn hơn 0.");

        RuleFor(v => v.BuyerId)
            .NotEqual(v => v.SellerId)
            .WithMessage("Người mua và người bán không được là cùng một người.");
    }
}
