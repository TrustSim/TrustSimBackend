using MediatR;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Requests;

/// <summary>
/// Command Người về nhì chấp nhận mua SIM với mức giá đặt cao nhất của họ sau khi Người thắng bùng cọc.
/// </summary>
public record AcceptRunnerUpOfferCommand(
    Guid AuctionId,
    Guid UserId = default
) : IRequest<AuctionResultDto>;
