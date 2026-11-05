using FluentValidation;
using TrustSimBackend.Application.Features.Chat.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Chat.Validators;

public class SendChatMessageCommandValidator : AbstractValidator<SendChatMessageCommand>
{
    public SendChatMessageCommandValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty().WithMessage("ID cuộc hội thoại không hợp lệ.");

        RuleFor(x => x.SenderId)
            .NotEmpty().WithMessage("Không xác định được người gửi tin nhắn.");

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Content) || (x.AttachmentFile != null && x.AttachmentFile.Length > 0))
            .WithMessage("Vui lòng nhập nội dung tin nhắn hoặc đính kèm tệp tin.");

        RuleFor(x => x.Content)
            .MaximumLength(4000).WithMessage("Nội dung tin nhắn không được vượt quá 4000 ký tự.");
    }
}
