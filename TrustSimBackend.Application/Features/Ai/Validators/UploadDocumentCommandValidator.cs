using FluentValidation;
using TrustSimBackend.Application.Features.Ai.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Ai.Validators;

public class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    public UploadDocumentCommandValidator()
    {
        RuleFor(x => x.File)
            .NotNull().WithMessage("Vui lòng đính kèm tệp tin tài liệu.")
            .Must(f => f.Length > 0).WithMessage("Tệp tin không được rỗng.")
            .Must(f => f.Length <= 50 * 1024 * 1024).WithMessage("Kích thước tệp không được vượt quá 50MB.");
    }
}
