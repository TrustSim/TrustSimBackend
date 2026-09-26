using MediatR;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Requests;

/// <summary>
/// Command chấp nhận mua ngay số SIM trong phiên đấu giá kiểu Hà Lan (Instant Dutch Buy).
/// </summary>
public record AcceptDutchBuyCommand(
    Guid AuctionId,
    Guid UserId = default
) : IRequest<AuctionResultDto>;
