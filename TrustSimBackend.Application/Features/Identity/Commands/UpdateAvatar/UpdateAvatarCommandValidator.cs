using FluentValidation;

namespace TrustSimBackend.Application.Features.Identity.Commands.UpdateAvatar;

/// <summary>
/// Validator kiểm tra tính hợp lệ khi cập nhật ảnh đại diện.
/// </summary>
public class UpdateAvatarCommandValidator : AbstractValidator<UpdateAvatarCommand>
{
    public UpdateAvatarCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId không hợp lệ.");

        RuleFor(x => x.AvatarUrl)
            .NotEmpty().WithMessage("Đường dẫn ảnh đại diện không được để trống.")
            .MaximumLength(500).WithMessage("Đường dẫn ảnh đại diện không được vượt quá 500 ký tự.");
    }
}
