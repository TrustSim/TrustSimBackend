using FluentValidation;

namespace TrustSimBackend.Application.Features.Identity.Commands.UpdateProfile;

/// <summary>
/// Validator kiểm tra tính hợp lệ khi cập nhật thông tin hồ sơ.
/// </summary>
public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId không hợp lệ.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MaximumLength(100).WithMessage("Họ và tên không được vượt quá 100 ký tự.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Matches(@"^(0[3|5|7|8|9])[0-9]{8}$").WithMessage("Số điện thoại không đúng định dạng di động Việt Nam (10 số).");

        RuleFor(x => x.IdentityNumber)
            .NotEmpty().WithMessage("Số CCCD/CMND không được để trống.")
            .Matches(@"^[0-9]{12}$").WithMessage("Số CCCD phải bao gồm chính xác 12 chữ số.");

        RuleFor(x => x.Address)
            .MaximumLength(200).WithMessage("Địa chỉ không được vượt quá 200 ký tự.");
    }
}
