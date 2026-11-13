namespace TrustSimBackend.Application.Common.Models;

/// <summary>
/// Cấu hình kết nối dịch vụ lưu trữ đối tượng MinIO (chuẩn AWS S3 API).
/// </summary>
public class MinioSettings
{
    public const string SectionName = "Minio";

    /// <summary>
    /// Địa chỉ Endpoint S3 nội bộ hoặc của MinIO container (vd: http://minio:9000 hoặc http://localhost:9000).
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:9000";

    /// <summary>
    /// Địa chỉ Endpoint công khai dùng để sinh URL cho trình duyệt tải/xem trực tiếp (vd: http://localhost:9000).
    /// </summary>
    public string PublicUrl { get; set; } = "http://localhost:9000";

    /// <summary>
    /// Access Key của MinIO (mặc định: minioadmin).
    /// </summary>
    public string AccessKey { get; set; } = "minioadmin";

    /// <summary>
    /// Secret Key của MinIO (mặc định: minioadmin).
    /// </summary>
    public string SecretKey { get; set; } = "minioadmin";

    /// <summary>
    /// Tên Bucket lưu trữ tệp (mặc định: pdf-files).
    /// </summary>
    public string Bucket { get; set; } = "pdf-files";

    /// <summary>
    /// Tên Bucket chuyên biệt dùng để lưu trữ hình ảnh gửi trong Chat (mặc định: chat-images).
    /// </summary>
    public string ChatImageBucket { get; set; } = "chat-images";

    /// <summary>
    /// Alias tương thích cho cấu hình ChatImagesBucket trong appsettings.json.
    /// </summary>
    public string ChatImagesBucket
    {
        get => ChatImageBucket;
        set => ChatImageBucket = value;
    }

    /// <summary>
    /// Thời hạn lưu trữ hình ảnh trong Chat (tính theo ngày) trước khi tự động xóa giải phóng rác (mặc định: 7 ngày = 1 tuần).
    /// </summary>
    public int ChatImageExpirationDays { get; set; } = 7;

    /// <summary>
    /// Alias tương thích cho cấu hình ChatImageRetentionDays trong appsettings.json.
    /// </summary>
    public int ChatImageRetentionDays
    {
        get => ChatImageExpirationDays;
        set => ChatImageExpirationDays = value;
    }

    /// <summary>
    /// Chu kỳ chạy tiến trình ngầm quét và dọn dẹp ảnh Chat quá hạn (tính theo phút, mặc định: 60 phút).
    /// </summary>
    public int ChatImageCleanupIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Sử dụng giao thức HTTPS bảo mật hay HTTP.
    /// </summary>
    public bool UseSsl { get; set; } = false;
}
