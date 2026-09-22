using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class AdminFreezeWalletCommandValidator : AbstractValidator<AdminFreezeWalletCommand>
{
    public AdminFreezeWalletCommandValidator()
    {
        RuleFor(v => v.WalletId)
            .NotEmpty().WithMessage("WalletId không được để trống.");
    }
}
