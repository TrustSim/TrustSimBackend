using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class ReleaseEscrowCommandValidator : AbstractValidator<ReleaseEscrowCommand>
{
    public ReleaseEscrowCommandValidator()
    {
        RuleFor(v => v.SellerUserId)
            .NotEmpty().WithMessage("SellerUserId không được để trống.");

        RuleFor(v => v.EscrowId)
            .NotEmpty().WithMessage("EscrowId không được để trống.");

        RuleFor(v => v.TotalEscrowAmount)
            .GreaterThan(0).WithMessage("Tổng số tiền giải ngân Escrow phải lớn hơn 0 VNĐ.")
            .LessThanOrEqualTo(50_000_000_000).WithMessage("Tổng số tiền giải ngân không vượt quá 50,000,000,000 VNĐ.")
            .Must(a => a == Math.Floor(a)).WithMessage("Tổng số tiền giải ngân phải là số nguyên VNĐ.");

        RuleFor(v => v.PlatformFeePercent)
            .InclusiveBetween(0.00m, 0.50m).WithMessage("Tỷ lệ phí sàn phải nằm trong khoảng từ 0% đến 50% (0.00 đến 0.50).");

        RuleFor(v => v.Description)
            .MaximumLength(500).WithMessage("Mô tả giải ngân không vượt quá 500 ký tự.")
            .When(v => !string.IsNullOrEmpty(v.Description));
    }
}
