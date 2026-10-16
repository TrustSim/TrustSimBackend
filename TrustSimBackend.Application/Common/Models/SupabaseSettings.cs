namespace TrustSimBackend.Application.Common.Models;

/// <summary>
/// Cấu hình kết nối dịch vụ lưu trữ Supabase Storage.
/// </summary>
public class SupabaseSettings
{
    public string Url { get; set; } = string.Empty;
    public string AnonKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = "contracts";
}
