using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Entities.Dispute;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Entities.System;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Persistence;

public class TrustSimDbContext : DbContext, IUnitOfWork
{
    public TrustSimDbContext(DbContextOptions<TrustSimDbContext> options) : base(options)
    {
    }

    #region Trade DbSets
    public DbSet<TradeTransaction> TradeTransactions => Set<TradeTransaction>();
    public DbSet<SimTransfer> SimTransfers => Set<SimTransfer>();
    public DbSet<TransferContract> TransferContracts => Set<TransferContract>();
    public DbSet<TransferProof> TransferProofs => Set<TransferProof>();
    public DbSet<DigitalSignature> DigitalSignatures => Set<DigitalSignature>();
    #endregion

    #region Identity DbSets
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<UserSecurityProfile> UserSecurityProfiles => Set<UserSecurityProfile>();
    public DbSet<IdentityVerification> IdentityVerifications => Set<IdentityVerification>();
    #endregion

    #region Sim DbSets
    public DbSet<SimNumber> SimNumbers => Set<SimNumber>();
    public DbSet<SimCarrier> SimCarriers => Set<SimCarrier>();
    public DbSet<SimCategory> SimCategories => Set<SimCategory>();
    public DbSet<SimCategoryMapping> SimCategoryMappings => Set<SimCategoryMapping>();
    public DbSet<SimOwnership> SimOwnerships => Set<SimOwnership>();
    public DbSet<SimVerification> SimVerifications => Set<SimVerification>();
    public DbSet<PhonePrefix> PhonePrefixes => Set<PhonePrefix>();
    public DbSet<FormatRhythm> FormatRhythms => Set<FormatRhythm>();
    #endregion

    #region Financial DbSets
    public DbSet<Escrow> Escrows => Set<Escrow>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<AuctionTransaction> AuctionTransactions => Set<AuctionTransaction>();
    #endregion

    #region Auction DbSets
    public DbSet<Auction> Auctions => Set<Auction>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<AuctionParticipant> AuctionParticipants => Set<AuctionParticipant>();
    public DbSet<AuctionDeposit> AuctionDeposits => Set<AuctionDeposit>();
    public DbSet<AuctionResult> AuctionResults => Set<AuctionResult>();
    public DbSet<AuctionRule> AuctionRules => Set<AuctionRule>();
    public DbSet<FraudAssessment> FraudAssessments => Set<FraudAssessment>();
    #endregion

    #region Dispute DbSets
    public DbSet<DisputeTicket> DisputeTickets => Set<DisputeTicket>();
    public DbSet<DisputeProof> DisputeProofs => Set<DisputeProof>();
    #endregion

    #region System DbSets
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    #endregion

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Tự động quét và áp dụng tất cả các file cấu hình Fluent API (IEntityTypeConfiguration) nằm trong Assembly này.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrustSimDbContext).Assembly);
    }
    /// <summary>
    /// Kích hoạt tự động chuyển đổi tên Entity và Property sang snake_case
    /// </summary>
    /// <param name="optionsBuilder"></param>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        
        // Kích hoạt tự động chuyển đổi tên Entity và Property sang snake_case
        optionsBuilder.UseSnakeCaseNamingConvention();
    }

    // Thực thi Interface IUnitOfWork
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Xử lý bắn Domain Events trước khi gọi base.SaveChangesAsync()
        return await base.SaveChangesAsync(cancellationToken);
    }
}
