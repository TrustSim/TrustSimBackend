using FluentValidation;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Identity.Validators;

public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(v => v.UserId)
            .NotEmpty().WithMessage("UserId không được để trống.");

        RuleFor(v => v.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MaximumLength(150).WithMessage("Họ và tên không vượt quá 150 ký tự.");

        RuleFor(v => v.PhoneNumber)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Matches(@"^(0|\+84)(3|5|7|8|9)[0-9]{8}$").WithMessage("Số điện thoại không đúng định dạng di động Việt Nam.");

        RuleFor(v => v.IdentityNumber)
            .NotEmpty().WithMessage("Số CCCD/CMND không được để trống.")
            .Matches(@"^([0-9]{9}|[0-9]{12})$").WithMessage("Số CCCD/CMND phải gồm 9 hoặc 12 chữ số.");
    }
}
