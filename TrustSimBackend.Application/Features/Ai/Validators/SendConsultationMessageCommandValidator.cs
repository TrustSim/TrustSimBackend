using FluentValidation;
using TrustSimBackend.Application.Features.Ai.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Ai.Validators;

public class SendConsultationMessageCommandValidator : AbstractValidator<SendConsultationMessageCommand>
{
    public SendConsultationMessageCommandValidator()
    {
        RuleFor(x => x.Request.Message)
            .NotEmpty().WithMessage("Nội dung tin nhắn không được để trống.")
            .MaximumLength(2000).WithMessage("Nội dung tin nhắn tối đa 2000 ký tự.");
    }
}
