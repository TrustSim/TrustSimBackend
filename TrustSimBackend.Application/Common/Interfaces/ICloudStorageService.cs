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
    /// Xóa tệp tin đã lưu trữ theo URL.
    /// </summary>
    Task<bool> DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default);
}
