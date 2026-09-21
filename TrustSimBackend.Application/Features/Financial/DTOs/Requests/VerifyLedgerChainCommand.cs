using MediatR;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Command thực thi quét và kiểm tra tính toàn vẹn chuỗi khối Hash Chain thời gian thực.
/// </summary>
public record VerifyLedgerChainCommand(
    long FromSequence = 1,
    int Limit = 10000
) : IRequest<LedgerIntegrityReportDto>;
