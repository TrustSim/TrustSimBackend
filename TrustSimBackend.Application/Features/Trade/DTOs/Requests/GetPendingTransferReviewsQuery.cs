using MediatR;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

/// <summary>
/// Query lấy danh sách các giao dịch có bằng chứng chuyển nhượng chờ Admin duyệt.
/// </summary>
public record GetPendingTransferReviewsQuery(
    Guid? AuctionId = null,
    string? Keyword = null
) : IRequest<List<PendingTransferReviewDto>>;

/// <summary>
/// Query lấy danh sách bằng chứng chuyển nhượng chờ Admin duyệt được gom nhóm theo Phiên đấu giá (Auction).
/// </summary>
public record GetPendingTransferReviewsGroupedByAuctionQuery(
    Guid? AuctionId = null,
    string? Keyword = null
) : IRequest<List<AuctionPendingTransferReviewGroupDto>>;
