using TrustSimBackend.Application.Features.Auction.Commands.CreateAuction;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Auction.Strategies;

/// <summary>
/// Strategy interface định nghĩa hành vi riêng biệt của từng loại hình đấu giá.
/// Áp dụng Strategy Pattern + Open/Closed Principle:
/// Thêm loại hình mới chỉ cần implement interface này, không cần sửa Factory hay Handler.
/// </summary>
public interface IAuctionStrategy
{
    /// <summary>Loại hình đấu giá mà strategy này xử lý.</summary>
    AuctionType AuctionType { get; }

    /// <summary>
    /// Kiểm tra tính hợp lệ của các tham số Rule theo loại hình đấu giá.
    /// Ném BadRequestException nếu không thỏa mãn điều kiện.
    /// </summary>
    void ValidateRule(CreateAuctionCommand request);

    /// <summary>
    /// Xây dựng thực thể AuctionRule phù hợp với loại hình đấu giá.
    /// Mỗi strategy sẽ set các trường liên quan, để 0 các trường không áp dụng.
    /// </summary>
    AuctionRule BuildRule(Guid auctionId, CreateAuctionCommand request);

    /// <summary>Giá khởi điểm ban đầu của phiên (có thể khác nhau theo loại).</summary>
    decimal GetInitialCurrentPrice(CreateAuctionCommand request);
}
