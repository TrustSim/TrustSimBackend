using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Query lấy danh sách lịch sử nạp/rút tiền của ví cá nhân.
/// </summary>
public record GetWalletTransactionsQuery(
    Guid UserId,
    WalletTransactionQueryFilter Filter
) : IRequest<PagedResult<WalletTransactionDto>>;
