using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Financial.Queries;

/// <summary>
/// Query lấy báo cáo đối soát toàn diện sổ cái kép và toàn vẹn số dư toàn sàn.
/// </summary>
public record GetLedgerIntegrityReportQuery() : IRequest<LedgerIntegrityReportDto>;

public class GetLedgerIntegrityReportQueryHandler : IRequestHandler<GetLedgerIntegrityReportQuery, LedgerIntegrityReportDto>
{
    private readonly ILedgerIntegrityVerifier _verifier;

    public GetLedgerIntegrityReportQueryHandler(ILedgerIntegrityVerifier verifier)
    {
        _verifier = verifier;
    }

    public async Task<LedgerIntegrityReportDto> Handle(GetLedgerIntegrityReportQuery request, CancellationToken cancellationToken)
    {
        return await _verifier.GenerateFullAuditReportAsync(cancellationToken);
    }
}
