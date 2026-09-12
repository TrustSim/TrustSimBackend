using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Auction.Commands.CreateAuction;
using TrustSimBackend.Application.Features.Auction.Strategies;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Auction.Factory;

/// <summary>
/// Factory Pattern: Đóng gói toàn bộ logic khởi tạo Auction Entity + AuctionRule.
/// 
/// Cách hoạt động:
/// 1. Nhận danh sách tất cả IAuctionStrategy đã đăng ký qua DI (IEnumerable).
/// 2. Tìm đúng strategy theo AuctionType trong command.
/// 3. Gọi strategy.ValidateRule() → ném exception nếu không hợp lệ.
/// 4. Khởi tạo Auction entity với các thông tin chung.
/// 5. Gọi strategy.BuildRule() để tạo AuctionRule phù hợp loại hình.
/// 6. Trả về tuple (Auction, AuctionRule) sẵn sàng để lưu DB.
/// 
/// Lợi ích (Open/Closed Principle):
/// Thêm loại hình mới (Sealed-bid) chỉ cần thêm Strategy mới + đăng ký DI,
/// không cần sửa Factory hay Handler.
/// </summary>
public class AuctionFactory
{
    private readonly IReadOnlyDictionary<AuctionType, IAuctionStrategy> _strategies;

    public AuctionFactory(IEnumerable<IAuctionStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(s => s.AuctionType);
    }

    /// <summary>
    /// Tạo cặp (Auction, AuctionRule) theo đúng loại hình đấu giá.
    /// </summary>
    /// <param name="request">Command đến từ Controller</param>
    /// <param name="auctionCode">Mã phiên đấu giá đã được tạo duy nhất từ Repository</param>
    /// <param name="authorId">ID người tạo phiên</param>
    /// <returns>Auction entity đã được liên kết với AuctionRule, sẵn sàng để persist</returns>
    public Domain.Entities.Auctions.Auction Create(
        CreateAuctionCommand request,
        string auctionCode,
        Guid authorId)
    {
        // 1. Chọn đúng strategy theo loại hình
        if (!_strategies.TryGetValue(request.AuctionType, out var strategy))
            throw new BadRequestException(
                $"Loại hình đấu giá '{request.AuctionType}' chưa được hỗ trợ.");

        // 2. Validate rule theo loại hình (mỗi strategy tự định nghĩa)
        strategy.ValidateRule(request);

        // 3. Tạo Auction entity với trạng thái ban đầu SCHEDULE
        var auctionId = Guid.NewGuid();
        var auction = new Domain.Entities.Auctions.Auction
        {
            Id = auctionId,
            AuctionCode = auctionCode,
            AuctionType = request.AuctionType,
            AuctionStatus = AuctionStatus.SCHEDULE,
            AuthorId = authorId,
            SimNumberId = request.SimNumberId,
            StartingPrice = request.StartingPrice,
            CurrentPrice = strategy.GetInitialCurrentPrice(request),
            ReservePrice = request.ReservePrice,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            RegistrationStartAt = request.RegistrationStartAt,
            RegistrationEndAt = request.RegistrationEndAt,
            LockVersion = 1,
            IsActive = true
        };

        // 4. Xây dựng AuctionRule theo strategy
        var rule = strategy.BuildRule(auctionId, request);
        auction.AuctionRule = rule;

        return auction;
    }
}
