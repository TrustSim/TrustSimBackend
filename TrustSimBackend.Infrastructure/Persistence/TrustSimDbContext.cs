using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Entities.Dispute;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Entities.System;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence.Interceptors;

namespace TrustSimBackend.Infrastructure.Persistence;

public class TrustSimDbContext : IdentityDbContext<UserAccount, IdentityRole<Guid>, Guid>
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
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
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
    public DbSet<LedgerJournal> LedgerJournals => Set<LedgerJournal>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<PaymentIntent> PaymentIntents => Set<PaymentIntent>();
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

    #region AI & RAG DbSets
    public DbSet<TrustSimBackend.Domain.Entities.Ai.DocumentChunk> DocumentChunks => Set<TrustSimBackend.Domain.Entities.Ai.DocumentChunk>();
    public DbSet<TrustSimBackend.Domain.Entities.Ai.SimValuation> SimValuations => Set<TrustSimBackend.Domain.Entities.Ai.SimValuation>();
    public DbSet<TrustSimBackend.Domain.Entities.Ai.AiConsultationSession> AiConsultationSessions => Set<TrustSimBackend.Domain.Entities.Ai.AiConsultationSession>();
    public DbSet<TrustSimBackend.Domain.Entities.Ai.AiConsultationMessage> AiConsultationMessages => Set<TrustSimBackend.Domain.Entities.Ai.AiConsultationMessage>();
    #endregion

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ánh xạ lại các bảng Identity về tên chuẩn snake_case
        modelBuilder.Entity<UserAccount>().ToTable("user_account");
        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("role");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("user_role");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claim");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_login");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claim");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_token");

        // Kích hoạt extension pgvector trên PostgreSQL
        modelBuilder.HasPostgresExtension("vector");

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

        // Kích hoạt bảo vệ tính bất biến của sổ cái (Append-Only)
        optionsBuilder.AddInterceptors(new LedgerImmutabilityInterceptor());
    }

    // Thực thi Interface IUnitOfWork
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Xử lý bắn Domain Events trước khi gọi base.SaveChangesAsync()
        return await base.SaveChangesAsync(cancellationToken);
    }
}
