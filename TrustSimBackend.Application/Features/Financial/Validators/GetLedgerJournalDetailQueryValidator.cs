using FluentValidation;
using TrustSimBackend.Application.Features.Financial.Queries;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class GetLedgerJournalDetailQueryValidator : AbstractValidator<GetLedgerJournalDetailQuery>
{
    public GetLedgerJournalDetailQueryValidator()
    {
        RuleFor(v => v.JournalId)
            .NotEmpty().WithMessage("JournalId không được để trống.");
    }
}
