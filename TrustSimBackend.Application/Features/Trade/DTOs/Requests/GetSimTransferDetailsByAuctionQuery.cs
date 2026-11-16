using MediatR;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

/// <summary>
/// Query lấy thông tin chi tiết tiến trình bàn giao SIM theo ID Phiên Đấu Giá (AuctionId).
/// </summary>
public record GetSimTransferDetailsByAuctionQuery(
    Guid AuctionId,
    Guid UserId,
    bool IsAdmin = false
) : IRequest<SimTransferDetailsDto>;

