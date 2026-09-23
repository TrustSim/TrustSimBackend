using MediatR;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Command MediatR xử lý tạo giao dịch nạp tiền vào ví.
/// </summary>
public record CreateDepositCommand(
    decimal Amount,
    string? Note,
    Guid UserId,
    string? IdempotencyKey = null
) : IRequest<DepositResponseDto>;

