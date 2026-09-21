using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Financial.Commands;

/// <summary>
/// Handler xử lý VerifyLedgerChainCommand (Quét và kiểm tra tính toàn vẹn chuỗi khối Hash Chain).
/// </summary>
public class VerifyLedgerChainCommandHandler : IRequestHandler<VerifyLedgerChainCommand, LedgerIntegrityReportDto>
{
    private readonly ILedgerIntegrityVerifier _verifier;

    public VerifyLedgerChainCommandHandler(ILedgerIntegrityVerifier verifier)
    {
        _verifier = verifier;
    }

    public async Task<LedgerIntegrityReportDto> Handle(VerifyLedgerChainCommand request, CancellationToken cancellationToken)
    {
        return await _verifier.VerifyChainAsync(request.FromSequence, request.Limit, cancellationToken);
    }
}
