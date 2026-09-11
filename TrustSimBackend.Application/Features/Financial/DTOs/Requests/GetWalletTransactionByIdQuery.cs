using MediatR;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Query xem chi tiết một giao dịch ví theo ID.
/// </summary>
public record GetWalletTransactionByIdQuery(
    Guid TransactionId,
    Guid UserId = default
) : IRequest<WalletTransactionDto>;
