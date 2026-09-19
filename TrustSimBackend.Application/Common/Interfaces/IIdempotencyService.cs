namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ kiểm soát chống trùng lặp yêu cầu tài chính (Idempotency Key Engine).
/// </summary>
public interface IIdempotencyService
{
    /// <summary>
    /// Kiểm tra xem Idempotency Key đã được xử lý thành công chưa.
    /// </summary>
    Task<bool> IsKeyProcessedAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đăng ký khóa Idempotency Key kèm kết quả xử lý.
    /// </summary>
    Task RegisterKeyAsync(string idempotencyKey, string resourceType, string resourceId, CancellationToken cancellationToken = default);
}
