using MediatR;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Command xử lý Nghiệp vụ 5: Khóa cọc tham gia đấu giá hoặc Nâng cọc bậc thang động (Nợ 2010 USER_AVAILABLE / Có 2020 USER_LOCKED).
/// Được thiết kế độc lập theo chuẩn Plug-and-Play để module Đấu giá (Auction) dispatch khi người dùng đăng ký hoặc nâng giá bid.
/// </summary>
public record LockAuctionDepositCommand(
    Guid UserId,
    decimal AmountToLock,
    Guid AuctionId,
    Guid? AuctionDepositId = null,
    string? Description = null
) : IRequest<LedgerJournalDto>;
