using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository quản lý vòng đời phiên đấu giá và các thực thể liên quan.
/// </summary>
public interface IAuctionRepository
{
    Task<Auction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Auction?> GetByCodeAsync(string auctionCode, CancellationToken cancellationToken = default);
    Task<Auction?> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsActiveAuctionForSimAsync(Guid simNumberId, CancellationToken cancellationToken = default);
    Task AddAsync(Auction auction, CancellationToken cancellationToken = default);
    void Update(Auction auction);

    // Phân trang cho Public/User (chỉ SCHEDULE + ACTIVE)
    Task<(List<Auction> Items, int TotalCount)> GetPublicPagedAsync(
        int pageIndex,
        int pageSize,
        AuctionType? auctionType = null,
        AuctionStatus? status = null,
        Guid? carrierId = null,
        Guid? categoryId = null,
        string? search = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        DateTime? startFrom = null,
        DateTime? startTo = null,
        CancellationToken cancellationToken = default);

    // Phân trang kho đấu giá của User (Author)
    Task<(List<Auction> Items, int TotalCount)> GetMyAuctionsPagedAsync(
        Guid authorId,
        int pageIndex,
        int pageSize,
        AuctionStatus? status = null,
        CancellationToken cancellationToken = default);

    // Phân trang toàn bộ cho Admin/Consultant
    Task<(List<Auction> Items, int TotalCount)> GetAdminPagedAsync(
        int pageIndex,
        int pageSize,
        AuctionType? auctionType = null,
        AuctionStatus? status = null,
        Guid? authorId = null,
        string? search = null,
        DateTime? startFrom = null,
        DateTime? startTo = null,
        CancellationToken cancellationToken = default);

    // Đếm số phiên đang ACTIVE của một người dùng
    Task<int> CountActiveAuctionsByAuthorAsync(Guid authorId, CancellationToken cancellationToken = default);

    // Tạo AuctionCode duy nhất
    Task<string> GenerateUniqueAuctionCodeAsync(CancellationToken cancellationToken = default);
}
