using Microsoft.AspNetCore.Http;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Hợp đồng dịch vụ lưu trữ hình ảnh và tệp tin.
/// </summary>
public interface ICloudStorageService
{
    /// <summary>
    /// Tải lên tệp hình ảnh và trả về URL truy cập an toàn.
    /// </summary>
    Task<string> UploadImageAsync(IFormFile file, string folder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tải lên mảng byte hình ảnh và trả về URL truy cập an toàn.
    /// </summary>
    Task<string> UploadImageBytesAsync(byte[] imageBytes, string fileName, string folder, string contentType = "image/jpeg", CancellationToken cancellationToken = default);

    /// <summary>
    /// Tải lên hình ảnh đính kèm trong đoạn chat vào Bucket ảnh chat riêng biệt (có chính sách tự động xóa giải phóng rác theo cấu hình).
    /// </summary>
    Task<string> UploadChatImageAsync(IFormFile file, Guid conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Quét và xóa các hình ảnh trong Bucket ảnh chat đã vượt quá thời hạn lưu trữ (ChatImageExpirationDays).
    /// </summary>
    Task<int> CleanupExpiredChatImagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Xóa tệp tin đã lưu trữ theo URL.
    /// </summary>
    Task<bool> DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dọn dẹp toàn bộ tệp tạm thời trong thư mục con của uploads (vd: kyc).
    /// </summary>
    Task<int> CleanAllTempUploadsAsync(string folder = "kyc", CancellationToken cancellationToken = default);
}
