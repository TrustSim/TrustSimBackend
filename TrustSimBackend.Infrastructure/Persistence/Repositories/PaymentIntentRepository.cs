using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

/// <summary>
/// Triển khai IPaymentIntentRepository sử dụng EF Core.
/// </summary>
public class PaymentIntentRepository : IPaymentIntentRepository
{
    private readonly TrustSimDbContext _context;

    public PaymentIntentRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<PaymentIntent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PaymentIntents
            .Include(p => p.UserAccount)
            .Include(p => p.Wallet)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<PaymentIntent?> GetByCodeAsync(string intentCode, CancellationToken cancellationToken = default)
    {
        return await _context.PaymentIntents
            .Include(p => p.UserAccount)
            .Include(p => p.Wallet)
            .FirstOrDefaultAsync(p => p.IntentCode == intentCode, cancellationToken);
    }

    public async Task<PaymentIntent?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return await _context.PaymentIntents
            .Include(p => p.UserAccount)
            .Include(p => p.Wallet)
            .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task<PaymentIntent?> GetByExternalTransactionIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        return await _context.PaymentIntents
            .Include(p => p.UserAccount)
            .Include(p => p.Wallet)
            .FirstOrDefaultAsync(p => p.ExternalTransactionId == externalId, cancellationToken);
    }

    public async Task<(IReadOnlyList<PaymentIntent> Items, int TotalCount)> GetByUserIdPagedAsync(
        Guid userId,
        PaymentIntentType? type = null,
        PaymentIntentStatus? status = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PaymentIntents
            .Where(p => p.UserId == userId)
            .AsNoTracking();

        if (type.HasValue)
        {
            query = query.Where(p => p.IntentType == type.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(p => p.CreatedDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(PaymentIntent intent, CancellationToken cancellationToken = default)
    {
        await _context.PaymentIntents.AddAsync(intent, cancellationToken);
    }

    public void Update(PaymentIntent intent)
    {
        _context.PaymentIntents.Update(intent);
    }
}
