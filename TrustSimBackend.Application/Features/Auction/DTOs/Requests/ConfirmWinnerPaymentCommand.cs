using MediatR;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Requests;

/// <summary>
/// Command người chiến thắng thực hiện thanh toán số tiền còn lại của phiên đấu giá trong thời hạn quy định.
/// </summary>
public record ConfirmWinnerPaymentCommand(
    Guid AuctionId,
    Guid UserId = default
) : IRequest<AuctionResultDto>;
