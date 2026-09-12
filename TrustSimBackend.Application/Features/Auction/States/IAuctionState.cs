using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Auction.States;

/// <summary>
/// State Pattern: Interface định nghĩa tất cả hành động có thể thực hiện trên phiên đấu giá.
/// Mỗi State Object sẽ cho phép hoặc từ chối (throw exception) tùy theo trạng thái hiện tại.
/// 
/// Vòng đời: DRAFT → SCHEDULE → ACTIVE → END / CANCELLED
/// 
/// Quy tắc: Phiên chỉ được kích hoạt khi SCHEDULE, chỉ được sửa khi SCHEDULE,
/// chỉ được đặt giá khi ACTIVE, v.v.
/// </summary>
public interface IAuctionState
{
    /// <summary>Trạng thái mà State Object này đại diện.</summary>
    AuctionStatus Status { get; }

    /// <summary>Cho phép Admin/Background Job kích hoạt phiên (SCHEDULE → ACTIVE).</summary>
    void Activate(Domain.Entities.Auctions.Auction auction);

    /// <summary>Cho phép Author/Admin hủy phiên.</summary>
    void Cancel(Domain.Entities.Auctions.Auction auction, string? reason, bool isAdmin);

    /// <summary>Cho phép hệ thống đóng phiên khi hết giờ (ACTIVE → END).</summary>
    void End(Domain.Entities.Auctions.Auction auction);

    /// <summary>Kiểm tra xem trạng thái hiện tại có cho phép đặt giá không.</summary>
    bool CanPlaceBid(Domain.Entities.Auctions.Auction auction);

    /// <summary>Kiểm tra xem thông tin phiên có thể được chỉnh sửa không.</summary>
    bool CanEdit(Domain.Entities.Auctions.Auction auction);
}
