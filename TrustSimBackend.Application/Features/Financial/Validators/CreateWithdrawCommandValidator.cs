using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class CreateWithdrawCommandValidator : AbstractValidator<CreateWithdrawCommand>
{
    public CreateWithdrawCommandValidator()
    {
        RuleFor(v => v.UserId)
            .NotEmpty().WithMessage("UserId không được để trống.");

        RuleFor(v => v.Amount)
            .GreaterThanOrEqualTo(50_000).WithMessage("Số tiền rút tối thiểu là 50,000 VNĐ.")
            .LessThanOrEqualTo(100_000_000).WithMessage("Số tiền rút tối đa mỗi lần là 100,000,000 VNĐ.");

        RuleFor(v => v.DestinationBankCode)
            .NotEmpty().WithMessage("Mã ngân hàng nhận không được để trống.")
            .MaximumLength(50).WithMessage("Mã ngân hàng không vượt quá 50 ký tự.");

        RuleFor(v => v.DestinationBankName)
            .NotEmpty().WithMessage("Tên ngân hàng nhận không được để trống.")
            .MaximumLength(150).WithMessage("Tên ngân hàng không vượt quá 150 ký tự.");

        RuleFor(v => v.DestinationAccountNumber)
            .NotEmpty().WithMessage("Số tài khoản nhận không được để trống.")
            .Matches(@"^[0-9A-Za-z]+$").WithMessage("Số tài khoản chỉ được chứa chữ cái và số.")
            .Length(6, 30).WithMessage("Số tài khoản phải từ 6 đến 30 ký tự.");

        RuleFor(v => v.DestinationAccountHolderName)
            .NotEmpty().WithMessage("Tên chủ tài khoản không được để trống.")
            .MaximumLength(150).WithMessage("Tên chủ tài khoản không vượt quá 150 ký tự.");

        RuleFor(v => v.Note)
            .MaximumLength(255).WithMessage("Ghi chú rút tiền không vượt quá 255 ký tự.");
    }
}
