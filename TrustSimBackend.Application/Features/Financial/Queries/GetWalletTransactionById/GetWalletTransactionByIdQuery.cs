using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs;

namespace TrustSimBackend.Application.Features.Financial.Queries.GetWalletTransactionById;

/// <summary>
/// Query lấy thông tin chi tiết một giao dịch ví theo ID.
/// </summary>
public record GetWalletTransactionByIdQuery(
    Guid TransactionId,
    Guid UserId
) : IRequest<ApiResponse<WalletTransactionDto>>;
