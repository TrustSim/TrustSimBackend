using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Query MediatR tra cứu lịch sử biến động doanh thu sàn và các đợt Admin rút tiền lợi nhuận.
/// </summary>
public record GetRevenueHistoryQuery(
    RevenueHistoryQueryFilter Filter
) : IRequest<PagedResult<PlatformRevenueHistoryDto>>;
