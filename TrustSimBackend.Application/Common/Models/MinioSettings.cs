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
    /// Sử dụng giao thức HTTPS bảo mật hay HTTP.
    /// </summary>
    public bool UseSsl { get; set; } = false;
}
