using FluentValidation;
using TrustSimBackend.Application.Features.Dispute.DTOs.Requests;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Dispute.Validators;

public class ResolveDisputeCommandValidator : AbstractValidator<ResolveDisputeCommand>
{
    public ResolveDisputeCommandValidator()
    {
        RuleFor(v => v.TicketId)
            .NotEmpty().WithMessage("TicketId không được để trống.");

        RuleFor(v => v.AdminId)
            .NotEmpty().WithMessage("AdminId không được để trống.");

        RuleFor(v => v.ResolutionStatus)
            .Must(s => s == DisputeStatus.RESOLVED_FAVOR_SELLER || s == DisputeStatus.RESOLVED_FAVOR_BUYER)
            .WithMessage("Trạng thái giải quyết phải là RESOLVED_FAVOR_SELLER hoặc RESOLVED_FAVOR_BUYER.");

        RuleFor(v => v.ResolutionNotes)
            .NotEmpty().WithMessage("Ghi chú giải quyết không được để trống.")
            .MinimumLength(10).WithMessage("Ghi chú giải quyết phải có ít nhất 10 ký tự.")
            .MaximumLength(2000).WithMessage("Ghi chú giải quyết không vượt quá 2000 ký tự.");
    }
}
