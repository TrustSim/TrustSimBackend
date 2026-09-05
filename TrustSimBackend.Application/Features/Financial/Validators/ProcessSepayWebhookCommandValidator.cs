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
            RuleFor(v => v.WebhookPayload.AccountNumber)
                .NotEmpty().WithMessage("AccountNumber trong webhook không được để trống.");

            RuleFor(v => v.WebhookPayload.TransferAmount)
                .GreaterThan(0).WithMessage("Số tiền chuyển khoản phải lớn hơn 0.");

            RuleFor(v => v.WebhookPayload.TransferType)
                .NotEmpty().WithMessage("TransferType trong webhook không được để trống.");

            RuleFor(v => v.WebhookPayload.Content)
                .NotEmpty().WithMessage("Nội dung chuyển khoản trong webhook không được để trống.");
        });

        RuleFor(v => v.AuthorizationHeader)
            .NotEmpty().WithMessage("Authorization header không được để trống.");
    }
}
