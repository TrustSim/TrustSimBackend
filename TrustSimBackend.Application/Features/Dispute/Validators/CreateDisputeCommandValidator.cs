using FluentValidation;
using TrustSimBackend.Application.Features.Dispute.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Dispute.Validators;

public class CreateDisputeCommandValidator : AbstractValidator<CreateDisputeCommand>
{
    public CreateDisputeCommandValidator()
    {
        RuleFor(v => v.TradeTransactionId)
            .NotEmpty().WithMessage("TradeTransactionId không được để trống.");

        RuleFor(v => v.ComplainantId)
            .NotEmpty().WithMessage("ComplainantId không được để trống.");

        RuleFor(v => v.Reason)
            .NotEmpty().WithMessage("Lý do khiếu nại không được để trống.")
            .MinimumLength(10).WithMessage("Lý do khiếu nại phải có ít nhất 10 ký tự.")
            .MaximumLength(1000).WithMessage("Lý do khiếu nại không vượt quá 1000 ký tự.");
    }
}
