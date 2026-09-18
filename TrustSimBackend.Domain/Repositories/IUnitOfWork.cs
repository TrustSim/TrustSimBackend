namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Unit of Work Interface quản lý tập trung tất cả Repositories và giao dịch dữ liệu.
/// </summary>
public interface IUnitOfWork
{
    // Repositories
    IUserAccountRepository Users { get; }
    IIdentityVerificationRepository IdentityVerifications { get; }
    IRefreshTokenRepository RefreshTokens { get; }
    IWalletRepository Wallets { get; }
    IWalletTransactionRepository WalletTransactions { get; }
    ISimCarrierRepository SimCarriers { get; }
    ISimCategoryRepository SimCategories { get; }
    ISimCategoryMappingRepository SimCategoryMappings { get; }
    IPhonePrefixRepository PhonePrefixes { get; }
    ISimNumberRepository SimNumbers { get; }
    ISimRepository Sims { get; }
    ISimOwnershipRepository SimOwnerships { get; }
    ISimVerificationRepository SimVerifications { get; }
    ISimMasterDataRepository SimMasterData { get; }
    IAuctionRepository Auctions { get; }
    IBidRepository Bids { get; }
    ITradeTransactionRepository TradeTransactions { get; }
    IDisputeTicketRepository DisputeTickets { get; }
    INotificationRepository Notifications { get; }
    IDocumentRepository Documents { get; }
    IFormatRhythmRepository FormatRhythms { get; }
    IAuditLogRepository AuditLogs { get; }
    IDocumentChunkRepository DocumentChunks { get; }
    ISimValuationRepository SimValuations { get; }
    IAiConsultationSessionRepository AiConsultationSessions { get; }

    // Lưu các thay đổi vào DB và trả về số dòng bị ảnh hưởng
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // Xóa theo dõi các Entity trong ChangeTracker khi gặp lỗi
    void ClearChangeTracker();

    // Quản lý Transaction
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}