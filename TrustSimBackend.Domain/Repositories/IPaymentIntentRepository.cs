using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý ý định thanh toán (PaymentIntent).
/// </summary>
public interface IPaymentIntentRepository
{
    Task<PaymentIntent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaymentIntent?> GetByCodeAsync(string intentCode, CancellationToken cancellationToken = default);
    Task<PaymentIntent?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task<PaymentIntent?> GetByExternalTransactionIdAsync(string externalId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<PaymentIntent> Items, int TotalCount)> GetByUserIdPagedAsync(
        Guid userId,
        PaymentIntentType? type = null,
        PaymentIntentStatus? status = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
    Task AddAsync(PaymentIntent intent, CancellationToken cancellationToken = default);
    void Update(PaymentIntent intent);
}
