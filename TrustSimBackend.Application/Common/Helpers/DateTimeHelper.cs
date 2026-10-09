namespace TrustSimBackend.Application.Common.Helpers;

/// <summary>
/// Tiện ích hỗ trợ xử lý và quy đổi múi giờ sang Giờ chuẩn Việt Nam (UTC+7 / GMT+7).
/// </summary>
public static class DateTimeHelper
{
    private static readonly TimeZoneInfo VietnamTimeZone = GetVietnamTimeZone();

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        try
        {
            // Windows ID: "SE Asia Standard Time", Linux/Docker/macOS IANA ID: "Asia/Ho_Chi_Minh"
            return TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");
        }
        catch
        {
            // Fallback nếu môi trường không nhận dạng được timezone ID chuẩn
            return TimeZoneInfo.CreateCustomTimeZone("UTC+07", TimeSpan.FromHours(7), "Vietnam Standard Time", "Vietnam Standard Time");
        }
    }

    /// <summary>
    /// Chuyển đổi một thời điểm UTC sang múi giờ Việt Nam (UTC+7).
    /// </summary>
    public static DateTime ToVietnamTime(this DateTime utcDateTime)
    {
        if (utcDateTime.Kind == DateTimeKind.Unspecified)
        {
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        }
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime.ToUniversalTime(), VietnamTimeZone);
    }

    /// <summary>
    /// Định dạng thời gian theo giờ Việt Nam cho tin nhắn thông báo hiển thị cho người dùng.
    /// </summary>
    public static string ToVietnamTimeString(this DateTime utcDateTime, string format = "HH:mm dd/MM/yyyy")
    {
        return utcDateTime.ToVietnamTime().ToString(format);
    }

    /// <summary>
    /// Định dạng thời gian nullable theo giờ Việt Nam.
    /// </summary>
    public static string ToVietnamTimeString(this DateTime? utcDateTime, string format = "HH:mm dd/MM/yyyy")
    {
        return utcDateTime.HasValue ? utcDateTime.Value.ToVietnamTimeString(format) : string.Empty;
    }
}
