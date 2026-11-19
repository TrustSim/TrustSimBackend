namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ quét định kỳ và gửi thông báo nhắc hạn cho các giao dịch hậu ký hợp đồng số.
/// </summary>
public interface ITradeDeadlineReminderService
{
    /// <summary>
    /// Quét các giao dịch đang trong giai đoạn chuyển nhượng/thanh lý và gửi thông báo nhắc hạn (chống gửi trùng).
    /// </summary>
    Task<int> ProcessDueRemindersAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}
