using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class AdminWithdrawProfitCommandValidator : AbstractValidator<AdminWithdrawProfitCommand>
{
    public AdminWithdrawProfitCommandValidator()
    {
        RuleFor(x => x.AdminUserId)
            .NotEmpty().WithMessage("ID của tài khoản Admin không được để trống.");

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(50_000).WithMessage("Số tiền rút lợi nhuận tối thiểu là 50.000 VNĐ.");

        RuleFor(x => x.DestinationBankCode)
            .NotEmpty().WithMessage("Mã ngân hàng nhận tiền không được để trống.")
            .MaximumLength(20).WithMessage("Mã ngân hàng không được vượt quá 20 ký tự.");

        RuleFor(x => x.DestinationBankName)
            .NotEmpty().WithMessage("Tên ngân hàng nhận tiền không được để trống.")
            .MaximumLength(100).WithMessage("Tên ngân hàng không được vượt quá 100 ký tự.");

        RuleFor(x => x.DestinationAccountNumber)
            .NotEmpty().WithMessage("Số tài khoản nhận tiền không được để trống.")
            .MaximumLength(50).WithMessage("Số tài khoản không được vượt quá 50 ký tự.")
            .Matches(@"^[0-9a-zA-Z]+$").WithMessage("Số tài khoản chỉ được chứa chữ cái và chữ số.");

        RuleFor(x => x.DestinationAccountHolderName)
            .NotEmpty().WithMessage("Tên chủ tài khoản nhận tiền không được để trống.")
            .MaximumLength(150).WithMessage("Tên chủ tài khoản không được vượt quá 150 ký tự.");
    }
}
