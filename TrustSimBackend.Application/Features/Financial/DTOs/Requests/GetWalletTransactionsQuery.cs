using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Query lấy danh sách lịch sử nạp/rút tiền của ví cá nhân.
/// </summary>
public record GetWalletTransactionsQuery(
    Guid UserId = default,
    WalletTransactionType? Type = null,
    TransactionStatus? Status = null,
    int PageIndex = 1,
    int PageSize = 10
) : IRequest<PagedResult<WalletTransactionDto>>;
