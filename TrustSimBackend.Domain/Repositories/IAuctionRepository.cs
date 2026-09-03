using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý Phiên đấu giá SIM (Auction).
/// </summary>
public interface IAuctionRepository
{
    Task<Auction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Auction?> GetByCodeAsync(string auctionCode, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Auction> Items, int TotalCount)> GetPagedAsync(
        string? keyword,
        AuctionStatus? status,
        AuctionType? type,
        Guid? authorId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string auctionCode, CancellationToken cancellationToken = default);
    Task AddAsync(Auction auction, CancellationToken cancellationToken = default);
    void Update(Auction auction);
    Task<AuctionParticipant?> GetParticipantAsync(Guid auctionId, Guid userId, CancellationToken cancellationToken = default);
    Task AddParticipantAsync(AuctionParticipant participant, CancellationToken cancellationToken = default);
    void UpdateParticipant(AuctionParticipant participant);
}
