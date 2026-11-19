namespace TrustSimBackend.Application.Common.Models;

/// <summary>
/// Cấu hình các mốc thời hạn giải quyết hợp đồng và tiến trình chuyển nhượng SIM.
/// </summary>
public class TradeDeadlineSettings
{
    public const string SectionName = "TradeDeadlineSettings";

    /// <summary>
    /// Thời hạn người mua đóng tiền ký quỹ Escrow (giờ, mặc định 48h).
    /// </summary>
    public int EscrowDeadlineHours { get; set; } = 48;

    /// <summary>
    /// Thời hạn hai bên hoàn tất ký Hợp đồng điện tử 3 bên (giờ, mặc định 24h - quá hạn coi như bùng kèo).
    /// </summary>
    public int SigningDeadlineHours { get; set; } = 24;

    /// <summary>
    /// Thời gian chờ khuyến nghị để hai bên hẹn ra điểm giao dịch nhà mạng (giờ, mặc định 48h).
    /// </summary>
    public int CarrierTransferWaitHours { get; set; } = 48;

    /// <summary>
    /// Thời hạn tối đa bên bán phải hoàn tất sang tên tại nhà mạng và nộp bằng chứng (ngày, mặc định 7 ngày).
    /// </summary>
    public int TransferDeadlineDays { get; set; } = 7;

    /// <summary>
    /// Thời hạn chuyển nhượng tính theo giờ.
    /// </summary>
    public int TransferDeadlineHours => TransferDeadlineDays * 24;

    /// <summary>
    /// Thời hạn người mua kiểm tra SIM và ký Biên bản nghiệm thu thanh lý (giờ, mặc định 24h).
    /// </summary>
    public int LiquidationSigningDeadlineHours { get; set; } = 24;

    /// <summary>
    /// Thời gian ân hạn trước khi kích hoạt cơ chế cưỡng chế tự động nếu bên bán chậm trễ (giờ, mặc định 24h).
    /// </summary>
    public int AutoEnforceGracePeriodHours { get; set; } = 24;

    /// <summary>
    /// Chu kỳ quét nhắc hạn định kỳ cho các giao dịch hậu ký hợp đồng (phút, mặc định 15 phút).
    /// </summary>
    public int ReminderCheckIntervalMinutes { get; set; } = 15;

    /// <summary>
    /// Các mốc giờ trước TransferDeadline để gửi thông báo nhắc hạn cho Người bán.
    /// </summary>
    public List<int> TransferReminderHoursBefore { get; set; } = new() { 24, 6 };

    /// <summary>
    /// Các mốc giờ trước LiquidationDeadline để gửi thông báo nhắc hạn nghiệm thu cho Người mua.
    /// </summary>
    public List<int> LiquidationReminderHoursBefore { get; set; } = new() { 12, 2 };

    /// <summary>
    /// Số giờ chờ duyệt tối đa trước khi gửi nhắc hạn cho Admin/Consultant.
    /// </summary>
    public int AdminPendingReviewReminderHours { get; set; } = 12;
}
