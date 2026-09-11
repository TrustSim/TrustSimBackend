using FluentValidation;
using TrustSimBackend.Application.Features.System.DTOs.Requests;

namespace TrustSimBackend.Application.Features.System.Validators;

public class MarkNotificationAsReadCommandValidator : AbstractValidator<MarkNotificationAsReadCommand>
{
    public MarkNotificationAsReadCommandValidator()
    {
        RuleFor(v => v.NotificationId)
            .NotEmpty().WithMessage("NotificationId không được để trống.");

        RuleFor(v => v.UserId)
            .NotEmpty().WithMessage("UserId không được để trống.");
    }
}
