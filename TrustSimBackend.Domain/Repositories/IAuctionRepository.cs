using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý Phiên đấu giá SIM (Auction).
/// </summary>
public interface IAuctionRepository
{
    Task<Auction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Auction?> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Auction?> GetByCodeAsync(string auctionCode, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Auction> Items, int TotalCount)> GetPagedAsync(
        string? keyword,
        AuctionStatus? status,
        AuctionType? type,
        Guid? authorId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Auction> Items, int TotalCount)> GetPublicPagedAsync(
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
    Task<(IReadOnlyList<Auction> Items, int TotalCount)> GetMyAuctionsPagedAsync(
        Guid authorId,
        int pageIndex,
        int pageSize,
        AuctionStatus? status = null,
        CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Auction> Items, int TotalCount)> GetAdminPagedAsync(
        int pageIndex,
        int pageSize,
        AuctionType? auctionType = null,
        AuctionStatus? status = null,
        Guid? authorId = null,
        string? search = null,
        DateTime? startFrom = null,
        DateTime? startTo = null,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string auctionCode, CancellationToken cancellationToken = default);
    Task<bool> ExistsActiveAuctionForSimAsync(Guid simNumberId, CancellationToken cancellationToken = default);
    Task<string> GenerateUniqueAuctionCodeAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Auction auction, CancellationToken cancellationToken = default);
    void Update(Auction auction);
    Task<AuctionParticipant?> GetParticipantAsync(Guid auctionId, Guid userId, CancellationToken cancellationToken = default);
    Task<AuctionParticipant?> GetParticipantByIdAsync(Guid participantId, CancellationToken cancellationToken = default);
    Task<bool> HasOverlappingRegisteredAuctionAsync(Guid userId, DateTime startAt, DateTime endAt, Guid? excludeAuctionId = null, CancellationToken cancellationToken = default);
    Task AddParticipantAsync(AuctionParticipant participant, CancellationToken cancellationToken = default);
    void UpdateParticipant(AuctionParticipant participant);
    Task<AuctionResult?> GetResultByAuctionIdAsync(Guid auctionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuctionResult>> GetPendingSettlementsForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddResultAsync(AuctionResult result, CancellationToken cancellationToken = default);
    void UpdateResult(AuctionResult result);
}
