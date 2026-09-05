using FluentValidation;
using TrustSimBackend.Application.Features.System.DTOs.Requests;

namespace TrustSimBackend.Application.Features.System.Validators;

public class UpdateDocumentCommandValidator : AbstractValidator<UpdateDocumentCommand>
{
    public UpdateDocumentCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty().WithMessage("Id tài liệu không được để trống.");

        RuleFor(v => v.DocumentName)
            .NotEmpty().WithMessage("Tên tài liệu không được để trống.")
            .MaximumLength(200).WithMessage("Tên tài liệu không vượt quá 200 ký tự.");

        RuleFor(v => v.LinkUrl)
            .NotEmpty().WithMessage("Đường dẫn tài liệu không được để trống.")
            .MaximumLength(500).WithMessage("Đường dẫn tài liệu không vượt quá 500 ký tự.")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("Đường dẫn tài liệu phải là URL hợp lệ.");
    }
}
