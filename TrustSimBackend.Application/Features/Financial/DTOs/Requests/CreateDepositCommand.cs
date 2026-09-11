using MediatR;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Command yêu cầu tạo giao dịch nạp tiền vào ví.
/// </summary>
public record CreateDepositCommand(
    decimal Amount,
    string? Note = null,
    Guid UserId = default
) : IRequest<DepositResponseDto>;
