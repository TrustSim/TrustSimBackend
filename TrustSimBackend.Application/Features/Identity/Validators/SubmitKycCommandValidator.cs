using FluentValidation;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Identity.Validators;

public class SubmitKycCommandValidator : AbstractValidator<SubmitKycCommand>
{
    public SubmitKycCommandValidator()
    {
        RuleFor(v => v.UserId)
            .NotEmpty().WithMessage("UserId không được để trống.");

        RuleFor(v => v.IdentityNumber)
            .NotEmpty().WithMessage("Số CCCD/CMND không được để trống.")
            .Matches(@"^([0-9]{9}|[0-9]{12})$").WithMessage("Số CCCD/CMND phải gồm chính xác 9 hoặc 12 chữ số.");

        RuleFor(v => v.AvatarImg)
            .MaximumLength(500).WithMessage("Đường dẫn ảnh đại diện không vượt quá 500 ký tự.")
            .When(v => !string.IsNullOrWhiteSpace(v.AvatarImg));
    }
}
