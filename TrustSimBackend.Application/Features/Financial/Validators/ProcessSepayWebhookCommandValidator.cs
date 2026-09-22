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
                .GreaterThan(0).WithMessage("Số tiền chuyển khoản phải lớn hơn 0.")
                .LessThanOrEqualTo(10_000_000_000).WithMessage("Số tiền chuyển khoản không hợp lệ (vượt quá 10 tỷ VNĐ).");

            RuleFor(v => v.WebhookPayload)
                .Must(p => !string.IsNullOrWhiteSpace(p.Content) || !string.IsNullOrWhiteSpace(p.Code))
                .WithMessage("Webhook phải chứa nội dung (Content) hoặc mã (Code) chuyển khoản.");

            RuleFor(v => v.WebhookPayload.Content)
                .MaximumLength(500).WithMessage("Nội dung chuyển khoản không vượt quá 500 ký tự.")
                .When(p => !string.IsNullOrEmpty(p.WebhookPayload.Content));
        });
    }
}

