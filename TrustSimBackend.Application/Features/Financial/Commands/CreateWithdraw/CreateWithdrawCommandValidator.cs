using FluentValidation;

namespace TrustSimBackend.Application.Features.Financial.Commands.CreateWithdraw;

/// <summary>
/// Validator kiểm tra tính hợp lệ của yêu cầu rút tiền.
/// </summary>
public class CreateWithdrawCommandValidator : AbstractValidator<CreateWithdrawCommand>
{
    public CreateWithdrawCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("ID người dùng không được để trống.");

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(5000)
            .WithMessage("Số tiền rút tối thiểu là 5.000 VNĐ.")
            .LessThanOrEqualTo(500000000)
            .WithMessage("Số tiền rút tối đa cho một lần giao dịch là 500.000.000 VNĐ.");

        RuleFor(x => x.DestinationBankCode)
            .NotEmpty().WithMessage("Mã ngân hàng nhận tiền không được để trống.")
            .MaximumLength(50).WithMessage("Mã ngân hàng tối đa 50 ký tự.");

        RuleFor(x => x.DestinationBankName)
            .NotEmpty().WithMessage("Tên ngân hàng nhận tiền không được để trống.")
            .MaximumLength(150).WithMessage("Tên ngân hàng tối đa 150 ký tự.");

        RuleFor(x => x.DestinationAccountNumber)
            .NotEmpty().WithMessage("Số tài khoản nhận tiền không được để trống.")
            .MaximumLength(50).WithMessage("Số tài khoản tối đa 50 ký tự.");

        RuleFor(x => x.DestinationAccountHolderName)
            .NotEmpty().WithMessage("Tên chủ tài khoản nhận tiền không được để trống.")
            .MaximumLength(150).WithMessage("Tên chủ tài khoản tối đa 150 ký tự.");
    }
}
