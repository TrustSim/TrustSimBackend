using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Query MediatR tra cứu lịch sử biến động doanh thu sàn và các đợt Admin rút tiền lợi nhuận.
/// </summary>
public record GetRevenueHistoryQuery(
    LedgerTransactionType? TransactionType = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int PageIndex = 1,
    int PageSize = 20
) : IRequest<PagedResult<PlatformRevenueHistoryDto>>;
