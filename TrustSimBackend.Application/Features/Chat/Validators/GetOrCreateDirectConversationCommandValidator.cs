using FluentValidation;
using TrustSimBackend.Application.Features.Chat.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Chat.Validators;

public class GetOrCreateDirectConversationCommandValidator : AbstractValidator<GetOrCreateDirectConversationCommand>
{
    public GetOrCreateDirectConversationCommandValidator()
    {
        RuleFor(x => x.CurrentUserId)
            .NotEmpty().WithMessage("Không xác định được tài khoản người dùng hiện tại.");

        RuleFor(x => x.TargetUserId)
            .NotEmpty().WithMessage("Vui lòng chọn người dùng cần nhắn tin.")
            .NotEqual(x => x.CurrentUserId).WithMessage("Không thể tự tạo cuộc hội thoại với chính mình.");

        RuleFor(x => x.InitialMessage)
            .MaximumLength(4000).WithMessage("Tin nhắn khởi tạo không được vượt quá 4000 ký tự.");
    }
}
