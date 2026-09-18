using FluentValidation;
using TrustSimBackend.Application.Features.Ai.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Ai.Validators;

public class IngestDocumentCommandValidator : AbstractValidator<IngestDocumentCommand>
{
    public IngestDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentName)
            .NotEmpty().WithMessage("Tên tài liệu không được để trống.")
            .MaximumLength(255).WithMessage("Tên tài liệu không vượt quá 255 ký tự.");

        RuleFor(x => x.LinkUrl)
            .NotEmpty().WithMessage("Đường dẫn (URL) tài liệu không được để trống.")
            .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out _))
            .WithMessage("Đường dẫn (URL) tài liệu không hợp lệ.");
    }
}
