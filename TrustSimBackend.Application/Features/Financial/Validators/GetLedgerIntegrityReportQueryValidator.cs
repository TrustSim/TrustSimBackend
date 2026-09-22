using FluentValidation;
using TrustSimBackend.Application.Features.Financial.Queries;

namespace TrustSimBackend.Application.Features.Financial.Validators;

public class GetLedgerIntegrityReportQueryValidator : AbstractValidator<GetLedgerIntegrityReportQuery>
{
    public GetLedgerIntegrityReportQueryValidator()
    {
        // Query không có tham số đầu vào, đảm bảo tuân thủ pipeline validation
    }
}
