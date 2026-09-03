using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Financial.Queries.GetWalletTransactions;

/// <summary>
/// Query lấy danh sách lịch sử nạp / rút tiền của ví có phân trang và bộ lọc.
/// </summary>
public record GetWalletTransactionsQuery(
    Guid UserId,
    WalletTransactionType? Type = null,
    TransactionStatus? Status = null,
    int PageIndex = 1,
    int PageSize = 10
) : IRequest<ApiResponse<PagedResult<WalletTransactionDto>>>;
