using FluentValidation;
using TrustSimBackend.Application.Features.Ai.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Ai.Validators;

public class EvaluateSimPriceCommandValidator : AbstractValidator<EvaluateSimPriceCommand>
{
    public EvaluateSimPriceCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => (x.SimNumberId.HasValue && x.SimNumberId != Guid.Empty) || !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Cần cung cấp SimNumberId hợp lệ hoặc chuỗi PhoneNumber để tiến hành định giá.");
    }
}
