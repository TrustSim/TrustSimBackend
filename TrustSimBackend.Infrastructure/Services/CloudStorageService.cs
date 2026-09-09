using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Interfaces;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Triển khai dịch vụ lưu trữ hình ảnh tài liệu eKYC.
/// </summary>
public class CloudStorageService : ICloudStorageService
{
    private readonly ILogger<CloudStorageService> _logger;
    private readonly string _baseStoragePath;

    public CloudStorageService(ILogger<CloudStorageService> logger)
    {
        _logger = logger;
        _baseStoragePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        if (!Directory.Exists(_baseStoragePath))
        {
            Directory.CreateDirectory(_baseStoragePath);
        }
    }

    public async Task<string> UploadImageAsync(IFormFile file, string folder, CancellationToken cancellationToken = default)
    {
        var targetDir = Path.Combine(_baseStoragePath, folder);
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext)) ext = ".jpg";
        var uniqueFileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(targetDir, uniqueFileName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream, cancellationToken);

        var relativeUrl = $"/uploads/{folder}/{uniqueFileName}".Replace("\\", "/");
        _logger.LogInformation("[Storage] Đã lưu tệp eKYC: {Url}", relativeUrl);

        return relativeUrl;
    }

    public async Task<string> UploadImageBytesAsync(byte[] imageBytes, string fileName, string folder, string contentType = "image/jpeg", CancellationToken cancellationToken = default)
    {
        var targetDir = Path.Combine(_baseStoragePath, folder);
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext)) ext = ".jpg";
        var uniqueFileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(targetDir, uniqueFileName);

        await File.WriteAllBytesAsync(fullPath, imageBytes, cancellationToken);

        var relativeUrl = $"/uploads/{folder}/{uniqueFileName}".Replace("\\", "/");
        _logger.LogInformation("[Storage] Đã lưu byte tệp eKYC: {Url}", relativeUrl);

        return relativeUrl;
    }

    public Task<bool> DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileUrl)) return Task.FromResult(false);

            var relative = fileUrl.TrimStart('/').Replace("uploads/", "");
            var fullPath = Path.Combine(_baseStoragePath, relative);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage] Lỗi khi xóa tệp: {Url}", fileUrl);
            return Task.FromResult(false);
        }
    }
}
