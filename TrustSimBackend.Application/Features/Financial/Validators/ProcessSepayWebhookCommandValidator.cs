using FluentValidation;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class ProcessSepayWebhookCommandValidator : AbstractValidator<ProcessSepayWebhookCommand>
{
    public ProcessSepayWebhookCommandValidator()
    {
        RuleFor(v => v.WebhookPayload)
            .NotNull().WithMessage("Dữ liệu webhook không được để trống.");

        When(v => v.WebhookPayload != null, () =>
        {
            RuleFor(v => v.WebhookPayload.TransferAmount)
                .GreaterThan(0).WithMessage("Số tiền chuyển khoản phải lớn hơn 0.");

            RuleFor(v => v.WebhookPayload)
                .Must(p => !string.IsNullOrWhiteSpace(p.Content) || !string.IsNullOrWhiteSpace(p.Code))
                .WithMessage("Webhook phải chứa nội dung (Content) hoặc mã (Code) chuyển khoản.");
        });
    }
}
