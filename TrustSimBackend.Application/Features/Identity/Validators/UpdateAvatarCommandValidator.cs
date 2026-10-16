using FluentValidation;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Identity.Validators;

public class UpdateAvatarCommandValidator : AbstractValidator<UpdateAvatarCommand>
{
    private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxFileSizeInBytes = 5 * 1024 * 1024; // 5 MB

    public UpdateAvatarCommandValidator()
    {
        RuleFor(v => v.UserId)
            .NotEmpty().WithMessage("UserId không được để trống.");

        RuleFor(v => v.File)
            .NotNull().WithMessage("Tệp hình ảnh avatar là bắt buộc.")
            .Must(file => file != null && file.Length > 0).WithMessage("Tệp ảnh không được để trống.")
            .Must(file => file != null && file.Length <= MaxFileSizeInBytes).WithMessage("Kích thước ảnh avatar tối đa là 5MB.")
            .Must(file => file != null && _allowedExtensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
            .WithMessage("Định dạng ảnh không hợp lệ (chỉ chấp nhận JPG, JPEG, PNG, WEBP).");
    }
}

