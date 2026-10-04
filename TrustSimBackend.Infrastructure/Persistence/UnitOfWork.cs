using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Persistence;

/// <summary>
/// Triển khai Unit of Work Pattern tập hợp tất cả Repositories và quản lý DbContext Transaction.
/// </summary>
public class UnitOfWork : IUnitOfWork, IAsyncDisposable, IDisposable
{
    private readonly TrustSimDbContext _context;
    private readonly IServiceProvider _serviceProvider;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(TrustSimDbContext context, IServiceProvider serviceProvider)
    {
        _context = context;
        _serviceProvider = serviceProvider;
    }

    public IUserAccountRepository Users => _serviceProvider.GetRequiredService<IUserAccountRepository>();
    public IIdentityVerificationRepository IdentityVerifications => _serviceProvider.GetRequiredService<IIdentityVerificationRepository>();
    public IRefreshTokenRepository RefreshTokens => _serviceProvider.GetRequiredService<IRefreshTokenRepository>();
    public IWalletRepository Wallets => _serviceProvider.GetRequiredService<IWalletRepository>();
    public IWalletTransactionRepository WalletTransactions => _serviceProvider.GetRequiredService<IWalletTransactionRepository>();
    public ISimCarrierRepository SimCarriers => _serviceProvider.GetRequiredService<ISimCarrierRepository>();
    public ISimCategoryRepository SimCategories => _serviceProvider.GetRequiredService<ISimCategoryRepository>();
    public ISimCategoryMappingRepository SimCategoryMappings => _serviceProvider.GetRequiredService<ISimCategoryMappingRepository>();
    public IPhonePrefixRepository PhonePrefixes => _serviceProvider.GetRequiredService<IPhonePrefixRepository>();
    public ISimNumberRepository SimNumbers => _serviceProvider.GetRequiredService<ISimNumberRepository>();
    public ISimRepository Sims => _serviceProvider.GetRequiredService<ISimRepository>();
    public ISimOwnershipRepository SimOwnerships => _serviceProvider.GetRequiredService<ISimOwnershipRepository>();
    public ISimVerificationRepository SimVerifications => _serviceProvider.GetRequiredService<ISimVerificationRepository>();
    public ISimMasterDataRepository SimMasterData => _serviceProvider.GetRequiredService<ISimMasterDataRepository>();
    public IAuctionRepository Auctions => _serviceProvider.GetRequiredService<IAuctionRepository>();
    public IBidRepository Bids => _serviceProvider.GetRequiredService<IBidRepository>();
    public ITradeTransactionRepository TradeTransactions => _serviceProvider.GetRequiredService<ITradeTransactionRepository>();
    public IDisputeTicketRepository DisputeTickets => _serviceProvider.GetRequiredService<IDisputeTicketRepository>();
    public INotificationRepository Notifications => _serviceProvider.GetRequiredService<INotificationRepository>();
    public IDocumentRepository Documents => _serviceProvider.GetRequiredService<IDocumentRepository>();
    public IFormatRhythmRepository FormatRhythms => _serviceProvider.GetRequiredService<IFormatRhythmRepository>();
    public IAuditLogRepository AuditLogs => _serviceProvider.GetRequiredService<IAuditLogRepository>();
    public IDocumentChunkRepository DocumentChunks => _serviceProvider.GetRequiredService<IDocumentChunkRepository>();
    public ISimValuationRepository SimValuations => _serviceProvider.GetRequiredService<ISimValuationRepository>();
    public IAiConsultationSessionRepository AiConsultationSessions => _serviceProvider.GetRequiredService<IAiConsultationSessionRepository>();
    public ILedgerJournalRepository LedgerJournals => _serviceProvider.GetRequiredService<ILedgerJournalRepository>();
    public ILedgerEntryRepository LedgerEntries => _serviceProvider.GetRequiredService<ILedgerEntryRepository>();
    public IPaymentIntentRepository PaymentIntents => _serviceProvider.GetRequiredService<IPaymentIntentRepository>();
    public IEscrowRepository Escrows => _serviceProvider.GetRequiredService<IEscrowRepository>();
    public IAuctionTransactionRepository AuctionTransactions => _serviceProvider.GetRequiredService<IAuctionTransactionRepository>();

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public void ClearChangeTracker()
    {
        _context.ChangeTracker.Clear();
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction != null)
        {
            await _transaction.DisposeAsync();
        }
    }
}
