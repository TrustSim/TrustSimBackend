using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class CreateWithdrawPhase1CommandValidator : AbstractValidator<CreateWithdrawPhase1Command>
{
    public CreateWithdrawPhase1CommandValidator()
    {
        RuleFor(v => v.UserId)
            .NotEmpty().WithMessage("UserId không được để trống.");

        RuleFor(v => v.Amount)
            .GreaterThanOrEqualTo(50_000).WithMessage("Số tiền rút tối thiểu là 50,000 VNĐ.")
            .LessThanOrEqualTo(100_000_000).WithMessage("Số tiền rút tối đa mỗi lần là 100,000,000 VNĐ.")
            .Must(a => a == Math.Floor(a)).WithMessage("Số tiền rút phải là số nguyên VNĐ.");

        RuleFor(v => v.DestinationBankCode)
            .NotEmpty().WithMessage("Mã ngân hàng nhận không được để trống.")
            .Matches(@"^[A-Za-z0-9_-]{2,20}$").WithMessage("Mã ngân hàng phải từ 2 đến 20 ký tự chữ hoặc số.")
            .MaximumLength(20).WithMessage("Mã ngân hàng không vượt quá 20 ký tự.");

        RuleFor(v => v.DestinationBankName)
            .NotEmpty().WithMessage("Tên ngân hàng nhận không được để trống.")
            .MaximumLength(150).WithMessage("Tên ngân hàng không vượt quá 150 ký tự.");

        RuleFor(v => v.DestinationAccountNumber)
            .NotEmpty().WithMessage("Số tài khoản nhận không được để trống.")
            .Matches(@"^[0-9A-Za-z]+$").WithMessage("Số tài khoản chỉ được chứa chữ cái và số.")
            .Length(6, 30).WithMessage("Số tài khoản phải từ 6 đến 30 ký tự.");

        RuleFor(v => v.DestinationAccountHolderName)
            .NotEmpty().WithMessage("Tên chủ tài khoản không được để trống.")
            .Matches(@"^[a-zA-ZÀ-ỹ\s]+$").WithMessage("Tên chủ tài khoản chỉ được chứa chữ cái và khoảng trắng.")
            .MaximumLength(150).WithMessage("Tên chủ tài khoản không vượt quá 150 ký tự.");

        RuleFor(v => v.Note)
            .MaximumLength(255).WithMessage("Ghi chú rút tiền không vượt quá 255 ký tự.")
            .When(v => !string.IsNullOrEmpty(v.Note));
    }
}
