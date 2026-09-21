using MediatR;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Command xử lý Nghiệp vụ 6: Hoàn cọc cho người không trúng đấu giá khi phiên kết thúc (Nợ 2020 USER_LOCKED / Có 2010 USER_AVAILABLE).
/// Được thiết kế độc lập theo chuẩn Plug-and-Play để module Đấu giá (Auction) dispatch khi kết thúc phiên.
/// </summary>
public record ReleaseAuctionDepositCommand(
    Guid UserId,
    decimal AmountToRelease,
    Guid AuctionId,
    Guid? AuctionDepositId = null,
    string? Reason = null
) : IRequest<LedgerJournalDto>;
