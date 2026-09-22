using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class GetWalletTransactionByIdQueryValidator : AbstractValidator<GetWalletTransactionByIdQuery>
{
    public GetWalletTransactionByIdQueryValidator()
    {
        RuleFor(v => v.TransactionId)
            .NotEmpty().WithMessage("TransactionId không được để trống.");
    }
}
