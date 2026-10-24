using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Requests;

/// <summary>
/// Query lấy danh sách các giao dịch / phiên đấu giá đang cần người dùng xử lý (Pending Settlements).
/// </summary>
public record GetMyPendingSettlementsQuery(
    Guid UserId,
    PendingSettlementQueryFilter Filter
) : IRequest<PagedResult<PendingSettlementDto>>;
