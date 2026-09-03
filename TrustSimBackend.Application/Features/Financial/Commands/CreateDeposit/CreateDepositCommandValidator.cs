using FluentValidation;

namespace TrustSimBackend.Application.Features.Financial.Commands.CreateDeposit;

/// <summary>
/// Validator kiểm tra tính hợp lệ của dữ liệu nạp tiền.
/// </summary>
public class CreateDepositCommandValidator : AbstractValidator<CreateDepositCommand>
{
    public CreateDepositCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("ID người dùng không được để trống.");

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(10000)
            .WithMessage("Số tiền nạp tối thiểu là 10.000 VNĐ.")
            .LessThanOrEqualTo(1000000000)
            .WithMessage("Số tiền nạp tối đa cho một giao dịch là 1.000.000.000 VNĐ.");
    }
}
