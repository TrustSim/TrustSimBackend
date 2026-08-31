using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Auction.States;

/// <summary>
/// State: SCHEDULE — Phiên đã được lên lịch, chờ đến giờ StartAt để kích hoạt.
/// Cho phép: Sửa thông tin, Hủy bởi Author/Admin, Kích hoạt (→ ACTIVE).
/// Không cho phép: Đặt giá.
/// </summary>
public class ScheduledAuctionState : IAuctionState
{
    public AuctionStatus Status => AuctionStatus.SCHEDULE;

    public void Activate(Domain.Entities.Auctions.Auction auction)
    {
        auction.AuctionStatus = AuctionStatus.ACTIVE;
    }

    public void Cancel(Domain.Entities.Auctions.Auction auction, string? reason, bool isAdmin)
    {
        // Cả Author và Admin đều có thể hủy khi SCHEDULE
        auction.AuctionStatus = AuctionStatus.CANCELLED;
        auction.IsActive = false;
    }

    public void End(Domain.Entities.Auctions.Auction auction)
    {
        // Phiên SCHEDULE chưa bắt đầu — không thể kết thúc trực tiếp
        throw new BadRequestException("Không thể kết thúc phiên đấu giá chưa được kích hoạt (SCHEDULE).");
    }

    public bool CanPlaceBid(Domain.Entities.Auctions.Auction auction) => false;

    public bool CanEdit(Domain.Entities.Auctions.Auction auction) => true;
}

/// <summary>
/// State: ACTIVE — Phiên đang diễn ra, nhận lượt đặt giá.
/// Cho phép: Đặt giá, Admin Hủy, Hệ thống kết thúc.
/// Không cho phép: Sửa thông tin, User thường hủy.
/// </summary>
public class ActiveAuctionState : IAuctionState
{
    public AuctionStatus Status => AuctionStatus.ACTIVE;

    public void Activate(Domain.Entities.Auctions.Auction auction)
    {
        throw new BadRequestException("Phiên đấu giá đang ACTIVE, không thể kích hoạt lại.");
    }

    public void Cancel(Domain.Entities.Auctions.Auction auction, string? reason, bool isAdmin)
    {
        if (!isAdmin)
            throw new ForbiddenAccessException("Phiên đấu giá đang diễn ra. Chỉ quản trị viên mới có quyền hủy.");

        auction.AuctionStatus = AuctionStatus.CANCELLED;
        auction.IsActive = false;
    }

    public void End(Domain.Entities.Auctions.Auction auction)
    {
        auction.AuctionStatus = AuctionStatus.END;
        auction.IsActive = false;
    }

    public bool CanPlaceBid(Domain.Entities.Auctions.Auction auction) => true;

    public bool CanEdit(Domain.Entities.Auctions.Auction auction) => false;
}

/// <summary>
/// State: END — Phiên đã kết thúc (hết giờ hoặc Dutch auction có người thắng).
/// Không cho phép bất kỳ hành động nào làm thay đổi trạng thái.
/// </summary>
public class EndedAuctionState : IAuctionState
{
    public AuctionStatus Status => AuctionStatus.END;

    public void Activate(Domain.Entities.Auctions.Auction auction)
        => throw new BadRequestException("Phiên đấu giá đã kết thúc.");

    public void Cancel(Domain.Entities.Auctions.Auction auction, string? reason, bool isAdmin)
        => throw new BadRequestException("Không thể hủy phiên đấu giá đã kết thúc.");

    public void End(Domain.Entities.Auctions.Auction auction)
        => throw new BadRequestException("Phiên đấu giá đã kết thúc.");

    public bool CanPlaceBid(Domain.Entities.Auctions.Auction auction) => false;

    public bool CanEdit(Domain.Entities.Auctions.Auction auction) => false;
}

/// <summary>
/// State: CANCELLED — Phiên đã bị hủy.
/// Trạng thái cuối, không thể thay đổi.
/// </summary>
public class CancelledAuctionState : IAuctionState
{
    public AuctionStatus Status => AuctionStatus.CANCELLED;

    public void Activate(Domain.Entities.Auctions.Auction auction)
        => throw new BadRequestException("Phiên đấu giá đã bị hủy, không thể kích hoạt.");

    public void Cancel(Domain.Entities.Auctions.Auction auction, string? reason, bool isAdmin)
        => throw new BadRequestException("Phiên đấu giá đã bị hủy trước đó.");

    public void End(Domain.Entities.Auctions.Auction auction)
        => throw new BadRequestException("Phiên đấu giá đã bị hủy.");

    public bool CanPlaceBid(Domain.Entities.Auctions.Auction auction) => false;

    public bool CanEdit(Domain.Entities.Auctions.Auction auction) => false;
}
