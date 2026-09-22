using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class SimulateTamperTestCommandValidator : AbstractValidator<SimulateTamperTestCommand>
{
    public SimulateTamperTestCommandValidator()
    {
        RuleFor(v => v.SequenceNumber)
            .GreaterThanOrEqualTo(0).WithMessage("Số thứ tự bản ghi kiểm tra (SequenceNumber) không được âm.");

        RuleFor(v => v.TamperedAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Số tiền giả lập sửa đổi không được âm.")
            .LessThanOrEqualTo(100_000_000_000).WithMessage("Số tiền giả lập không vượt quá 100 tỷ VNĐ.");
    }
}
