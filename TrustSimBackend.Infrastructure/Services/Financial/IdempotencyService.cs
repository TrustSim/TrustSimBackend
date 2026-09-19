using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Services.Financial;

/// <summary>
/// Triển khai dịch vụ chống trùng lặp yêu cầu tài chính (Idempotency Key Engine).
/// </summary>
public class IdempotencyService : IIdempotencyService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<IdempotencyService> _logger;
    private static readonly ConcurrentDictionary<string, (string ResourceType, string ResourceId, DateTime Timestamp)> _memoryCache = new();

    public IdempotencyService(
        IUnitOfWork unitOfWork,
        ILogger<IdempotencyService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> IsKeyProcessedAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return false;

        // 1. Kiểm tra trong bộ nhớ nhanh
        if (_memoryCache.ContainsKey(idempotencyKey))
        {
            return true;
        }

        // 2. Kiểm tra trong Database qua PaymentIntent hoặc LedgerJournal
        var existingIntent = await _unitOfWork.PaymentIntents.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existingIntent != null)
        {
            _memoryCache.TryAdd(idempotencyKey, ("PaymentIntent", existingIntent.Id.ToString(), DateTime.UtcNow));
            return true;
        }

        var existingJournal = await _unitOfWork.LedgerJournals.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existingJournal != null)
        {
            _memoryCache.TryAdd(idempotencyKey, ("LedgerJournal", existingJournal.Id.ToString(), DateTime.UtcNow));
            return true;
        }

        return false;
    }

    public Task RegisterKeyAsync(string idempotencyKey, string resourceType, string resourceId, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            _memoryCache.TryAdd(idempotencyKey, (resourceType, resourceId, DateTime.UtcNow));
            _logger.LogInformation("[Idempotency] Đã đăng ký khóa '{Key}' cho '{Type}' ID='{Id}'", idempotencyKey, resourceType, resourceId);
        }

        return Task.CompletedTask;
    }
}
