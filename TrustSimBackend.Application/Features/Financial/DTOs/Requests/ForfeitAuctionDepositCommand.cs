using MediatR;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Command xử lý Nghiệp vụ 7: Tịch thu cọc vi phạm quy chế đấu giá (Nợ 2020 USER_LOCKED / Có 4020 PLATFORM_PENALTY_REVENUE).
/// Được thiết kế độc lập theo chuẩn Plug-and-Play để xử lý khi người thắng bỏ cọc, vi phạm thời gian ký quỹ Escrow.
/// </summary>
public record ForfeitAuctionDepositCommand(
    Guid UserId,
    decimal AmountToForfeit,
    Guid AuctionId,
    Guid? AuctionDepositId = null,
    string? ViolationReason = null
) : IRequest<LedgerJournalDto>;
