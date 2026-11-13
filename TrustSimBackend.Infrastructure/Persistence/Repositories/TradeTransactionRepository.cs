using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence.Extensions;
using TrustSimBackend.Infrastructure.Persistence.SortMappings;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

public class TradeTransactionRepository : ITradeTransactionRepository
{
    private readonly TrustSimDbContext _dbContext;

    public TradeTransactionRepository(TrustSimDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(TradeTransaction tradeTransaction, CancellationToken cancellationToken = default)
    {
        await _dbContext.TradeTransactions.AddAsync(tradeTransaction, cancellationToken);
    }

    public async Task AddTransferContractAsync(TransferContract contract, CancellationToken cancellationToken = default)
    {
        await _dbContext.TransferContracts.AddAsync(contract, cancellationToken);
    }

    public async Task AddDigitalSignatureAsync(DigitalSignature signature, CancellationToken cancellationToken = default)
    {
        await _dbContext.DigitalSignatures.AddAsync(signature, cancellationToken);
    }

    public async Task AddSimTransferAsync(SimTransfer simTransfer, CancellationToken cancellationToken = default)
    {
        await _dbContext.SimTransfers.AddAsync(simTransfer, cancellationToken);
    }

    public async Task AddTransferProofAsync(TransferProof transferProof, CancellationToken cancellationToken = default)
    {
        await _dbContext.TransferProofs.AddAsync(transferProof, cancellationToken);
    }

    public async Task<TradeTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TradeTransactions
            .Include(t => t.Buyer)
                .ThenInclude(u => u!.UserProfile)
            .Include(t => t.Seller)
                .ThenInclude(u => u!.UserProfile)
            .Include(t => t.SimNumber)
                .ThenInclude(s => s!.PhonePrefix)
                    .ThenInclude(p => p!.SimCarrier)
            .Include(t => t.Escrow)
            .Include(t => t.Auction)
                .ThenInclude(a => a!.AuctionRule)
            .Include(t => t.TransferContract)
                .ThenInclude(c => c!.DigitalSignatures)
            .Include(t => t.SimTransfer)
                .ThenInclude(st => st!.TransferProofs)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<TradeTransaction?> GetByCodeAsync(string tradeCode, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TradeTransactions
            .Include(t => t.Buyer)
                .ThenInclude(u => u!.UserProfile)
            .Include(t => t.Seller)
                .ThenInclude(u => u!.UserProfile)
            .Include(t => t.SimNumber)
                .ThenInclude(s => s!.PhonePrefix)
                    .ThenInclude(p => p!.SimCarrier)
            .Include(t => t.Escrow)
            .Include(t => t.Auction)
                .ThenInclude(a => a!.AuctionRule)
            .Include(t => t.TransferContract)
                .ThenInclude(c => c!.DigitalSignatures)
            .Include(t => t.SimTransfer)
                .ThenInclude(st => st!.TransferProofs)
            .FirstOrDefaultAsync(t => t.TradeTransactionCode == tradeCode, cancellationToken);
    }

    public async Task<(IReadOnlyList<TradeTransaction> Items, int TotalCount)> GetPagedAsync(
        AdminTradeQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.TradeTransactions
            .Include(t => t.Buyer)
                .ThenInclude(u => u!.UserProfile)
            .Include(t => t.Seller)
                .ThenInclude(u => u!.UserProfile)
            .Include(t => t.SimNumber)
            .Include(t => t.Escrow)
            .Include(t => t.TransferContract)
            .Include(t => t.SimTransfer)
                .ThenInclude(st => st!.TransferProofs)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var clean = filter.Keyword.Trim();
            query = query.Where(t => t.TradeTransactionCode.Contains(clean)
                || (t.SimNumber != null && t.SimNumber.PhoneNumber.Contains(clean))
                || (t.Buyer != null && t.Buyer.UserProfile != null && t.Buyer.UserProfile.FullName.Contains(clean))
                || (t.Seller != null && t.Seller.UserProfile != null && t.Seller.UserProfile.FullName.Contains(clean)));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(t => t.TradeTransactionStatus == filter.Status.Value);
        }

        if (filter.BuyerId.HasValue)
        {
            query = query.Where(t => t.BuyerId == filter.BuyerId.Value);
        }

        if (filter.SellerId.HasValue)
        {
            query = query.Where(t => t.SellerId == filter.SellerId.Value);
        }

        if (filter.MinAmount.HasValue)
        {
            query = query.Where(t => t.Escrow != null && t.Escrow.EscrowAmount >= filter.MinAmount.Value);
        }

        if (filter.MaxAmount.HasValue)
        {
            query = query.Where(t => t.Escrow != null && t.Escrow.EscrowAmount <= filter.MaxAmount.Value);
        }

        query = query.ApplyDateRange(t => t.CreatedDate, filter.FromDate, filter.ToDate);
        query = query.ApplySafeSorting(TradeSortMappings.Mappings, filter.SortBy, filter.SortOrder, defaultSortExpression: t => t.CreatedDate, defaultSortOrder: SortOrder.Desc);

        return await query.ToPagedTupleAsync(filter.PageIndex, filter.PageSize, cancellationToken);
    }

    public async Task<(IReadOnlyList<TradeTransaction> Items, int TotalCount)> GetPagedByUserIdAsync(
        Guid userId,
        MyTradeQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.TradeTransactions
            .Include(t => t.Buyer)
                .ThenInclude(u => u!.UserProfile)
            .Include(t => t.Seller)
                .ThenInclude(u => u!.UserProfile)
            .Include(t => t.SimNumber)
            .Include(t => t.Escrow)
            .Include(t => t.TransferContract)
            .Include(t => t.SimTransfer)
                .ThenInclude(st => st!.TransferProofs)
            .AsQueryable();

        if (string.Equals(filter.Role, "buyer", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(t => t.BuyerId == userId);
        }
        else if (string.Equals(filter.Role, "seller", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(t => t.SellerId == userId);
        }
        else
        {
            query = query.Where(t => t.BuyerId == userId || t.SellerId == userId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var clean = filter.Keyword.Trim();
            query = query.Where(t => t.TradeTransactionCode.Contains(clean)
                || (t.SimNumber != null && t.SimNumber.PhoneNumber.Contains(clean))
                || (t.Buyer != null && t.Buyer.UserProfile != null && t.Buyer.UserProfile.FullName.Contains(clean))
                || (t.Seller != null && t.Seller.UserProfile != null && t.Seller.UserProfile.FullName.Contains(clean)));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(t => t.TradeTransactionStatus == filter.Status.Value);
        }

        if (filter.MinAmount.HasValue)
        {
            query = query.Where(t => t.Escrow != null && t.Escrow.EscrowAmount >= filter.MinAmount.Value);
        }

        if (filter.MaxAmount.HasValue)
        {
            query = query.Where(t => t.Escrow != null && t.Escrow.EscrowAmount <= filter.MaxAmount.Value);
        }

        query = query.ApplyDateRange(t => t.CreatedDate, filter.FromDate, filter.ToDate);
        query = query.ApplySafeSorting(TradeSortMappings.Mappings, filter.SortBy, filter.SortOrder, defaultSortExpression: t => t.CreatedDate, defaultSortOrder: SortOrder.Desc);

        return await query.ToPagedTupleAsync(filter.PageIndex, filter.PageSize, cancellationToken);
    }

    public void Update(TradeTransaction tradeTransaction)
    {
        _dbContext.TradeTransactions.Update(tradeTransaction);
    }

    public async Task<IReadOnlyList<TradeTransaction>> GetPendingReviewsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.TradeTransactions
            .Include(t => t.Buyer)
                .ThenInclude(u => u!.UserProfile)
            .Include(t => t.Seller)
                .ThenInclude(u => u!.UserProfile)
            .Include(t => t.SimNumber)
                .ThenInclude(s => s!.PhonePrefix)
                    .ThenInclude(p => p!.SimCarrier)
            .Include(t => t.Escrow)
            .Include(t => t.Auction)
            .Include(t => t.TransferContract)
            .Include(t => t.SimTransfer)
                .ThenInclude(st => st!.TransferProofs)
            .Where(t => t.SimTransfer != null &&
                t.SimTransfer.SimTransferStatus != SimTransferStatus.COMPLETED &&
                t.SimTransfer.SimTransferStatus != SimTransferStatus.CANCELLED &&
                (t.SimTransfer.SimTransferStatus == SimTransferStatus.SELLER_PROOF_SUBMITTED ||
                 t.SimTransfer.SimTransferStatus == SimTransferStatus.BUYER_PROOF_SUBMITTED ||
                 t.SimTransfer.TransferProofs.Any(p => p.ProofStatus == ProofStatus.PENDING)))
            .OrderByDescending(t => t.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TradeTransaction>> GetExpiredEscrowTransactionsAsync(DateTime currentTime)
    {
        return await _dbContext.TradeTransactions
            .Include(t => t.Escrow)
            .Where(t => t.TradeTransactionStatus == TradeTransactionStatus.PendingEscrow 
                     && t.EscrowDeadline < currentTime)
            .ToListAsync();
    }
}