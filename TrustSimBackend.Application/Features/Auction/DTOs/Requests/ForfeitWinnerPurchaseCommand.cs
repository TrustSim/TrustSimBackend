using MediatR;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Requests;

/// <summary>
/// Command Người thắng chủ động từ bỏ quyền mua SIM sau khi kết thúc phiên (chấp nhận bị phạt cọc, chuyển quyền mua sang Runner-Up ngay lập tức).
/// </summary>
public record ForfeitWinnerPurchaseCommand(
    Guid AuctionId,
    Guid UserId = default,
    string? Reason = null
) : IRequest<AuctionResultDto>;
