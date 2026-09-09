using FluentValidation;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Identity.Validators;

public class SubmitEkycCommandValidator : AbstractValidator<SubmitEkycVerificationCommand>
{
    private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxFileSizeInBytes = 5 * 1024 * 1024; // 5 MB

    public SubmitEkycCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId không được để trống.");

        RuleFor(x => x.FrontCardImage)
            .NotNull().WithMessage("Ảnh mặt trước CMND/CCCD là bắt buộc.")
            .Must(file => file != null && file.Length > 0).WithMessage("Ảnh mặt trước không được rỗng.")
            .Must(file => file != null && file.Length <= MaxFileSizeInBytes).WithMessage("Kích thước ảnh mặt trước tối đa là 5MB.")
            .Must(file => file != null && _allowedExtensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
            .WithMessage("Định dạng ảnh mặt trước không hợp lệ (chỉ chấp nhận JPG, JPEG, PNG, WEBP).");

        RuleFor(x => x.SelfieImage)
            .NotNull().WithMessage("Ảnh chân dung selfie là bắt buộc.")
            .Must(file => file != null && file.Length > 0).WithMessage("Ảnh chân dung selfie không được rỗng.")
            .Must(file => file != null && file.Length <= MaxFileSizeInBytes).WithMessage("Kích thước ảnh selfie tối đa là 5MB.")
            .Must(file => file != null && _allowedExtensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
            .WithMessage("Định dạng ảnh selfie không hợp lệ (chỉ chấp nhận JPG, JPEG, PNG, WEBP).");

        When(x => x.BackCardImage != null, () =>
        {
            RuleFor(x => x.BackCardImage!)
                .Must(file => file.Length <= MaxFileSizeInBytes).WithMessage("Kích thước ảnh mặt sau tối đa là 5MB.")
                .Must(file => _allowedExtensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
                .WithMessage("Định dạng ảnh mặt sau không hợp lệ (chỉ chấp nhận JPG, JPEG, PNG, WEBP).");
        });
    }
}
