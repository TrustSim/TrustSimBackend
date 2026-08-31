using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Auction.States;

/// <summary>
/// State Context: Ánh xạ AuctionStatus (enum) → IAuctionState (object) tương ứng.
/// Các Command Handler gọi AuctionStateContext.GetState(status) để lấy đúng State,
/// sau đó gọi action (Activate/Cancel/End...) — State tự xử lý kiểm tra tính hợp lệ.
/// 
/// Ví dụ sử dụng:
/// <code>
/// var state = AuctionStateContext.GetState(auction.AuctionStatus);
/// state.Cancel(auction, reason, isAdmin);  // Sẽ throw nếu không hợp lệ
/// </code>
/// </summary>
public static class AuctionStateContext
{
    private static readonly Dictionary<AuctionStatus, IAuctionState> _states = new()
    {
        [AuctionStatus.SCHEDULE]  = new ScheduledAuctionState(),
        [AuctionStatus.ACTIVE]    = new ActiveAuctionState(),
        [AuctionStatus.END]       = new EndedAuctionState(),
        [AuctionStatus.CANCELLED] = new CancelledAuctionState(),
    };

    /// <summary>
    /// Lấy State Object tương ứng với trạng thái hiện tại của phiên đấu giá.
    /// </summary>
    /// <exception cref="BadRequestException">Khi trạng thái không được hỗ trợ.</exception>
    public static IAuctionState GetState(AuctionStatus status)
    {
        if (_states.TryGetValue(status, out var state))
            return state;

        throw new BadRequestException($"Trạng thái phiên đấu giá '{status}' không được hỗ trợ.");
    }

    /// <summary>Kiểm tra nhanh xem trạng thái hiện tại có thể đặt giá không.</summary>
    public static bool CanPlaceBid(AuctionStatus status)
        => _states.TryGetValue(status, out var s) && s.CanPlaceBid(null!);

    /// <summary>Kiểm tra nhanh xem trạng thái hiện tại có thể chỉnh sửa không.</summary>
    public static bool CanEdit(AuctionStatus status)
        => _states.TryGetValue(status, out var s) && s.CanEdit(null!);
}
