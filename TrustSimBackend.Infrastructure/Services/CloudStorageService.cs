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
        _baseStoragePath = ResolveBaseStoragePath();
        if (!Directory.Exists(_baseStoragePath))
        {
            Directory.CreateDirectory(_baseStoragePath);
        }
    }

    private static string ResolveBaseStoragePath()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads"),
            Path.Combine(Directory.GetCurrentDirectory(), "TrustSimBackend.API", "wwwroot", "uploads"),
            Path.Combine(AppContext.BaseDirectory, "wwwroot", "uploads"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "TrustSimBackend.API", "wwwroot", "uploads"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "TrustSimBackend.API", "wwwroot", "uploads")
        };

        foreach (var path in candidates)
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                var parentDir = Directory.GetParent(fullPath)?.FullName;
                if (Directory.Exists(fullPath) || (parentDir != null && Directory.Exists(parentDir)))
                {
                    if (!Directory.Exists(fullPath))
                    {
                        Directory.CreateDirectory(fullPath);
                    }
                    return fullPath;
                }
            }
            catch
            {
                // Ignore invalid path probes
            }
        }

        var defaultPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        if (!Directory.Exists(defaultPath))
        {
            Directory.CreateDirectory(defaultPath);
        }
        return defaultPath;
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
        _logger.LogInformation("[Storage] Đã lưu tệp tạm eKYC: {Url} tại {Path}", relativeUrl, fullPath);

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
        _logger.LogInformation("[Storage] Đã lưu byte tệp tạm eKYC: {Url} tại {Path}", relativeUrl, fullPath);

        return relativeUrl;
    }

    public Task<bool> DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileUrl)) return Task.FromResult(false);

            var normalized = fileUrl.Replace('\\', '/').TrimStart('/');
            if (normalized.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring("uploads/".Length);
            }

            var relativeFilePath = normalized.Replace('/', Path.DirectorySeparatorChar);
            var candidatePaths = new List<string>
            {
                Path.Combine(_baseStoragePath, relativeFilePath),
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", relativeFilePath),
                Path.Combine(Directory.GetCurrentDirectory(), "TrustSimBackend.API", "wwwroot", "uploads", relativeFilePath),
                Path.Combine(AppContext.BaseDirectory, "wwwroot", "uploads", relativeFilePath),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "TrustSimBackend.API", "wwwroot", "uploads", relativeFilePath),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "TrustSimBackend.API", "wwwroot", "uploads", relativeFilePath)
            };

            bool deleted = false;
            foreach (var path in candidatePaths)
            {
                try
                {
                    var fullPath = Path.GetFullPath(path);
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                        _logger.LogInformation("[Storage] Đã xóa thành công tệp ảnh eKYC sau xác thực: {Path}", fullPath);
                        deleted = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Storage] Cảnh báo khi xóa tệp tại: {Path}", path);
                }
            }

            return Task.FromResult(deleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage] Lỗi khi xử lý xóa tệp eKYC: {Url}", fileUrl);
            return Task.FromResult(false);
        }
    }

    public Task<int> CleanAllTempUploadsAsync(string folder = "kyc", CancellationToken cancellationToken = default)
    {
        int deletedCount = 0;
        try
        {
            var candidateDirs = new[]
            {
                Path.Combine(_baseStoragePath, folder),
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", folder),
                Path.Combine(Directory.GetCurrentDirectory(), "TrustSimBackend.API", "wwwroot", "uploads", folder),
                Path.Combine(AppContext.BaseDirectory, "wwwroot", "uploads", folder)
            };

            foreach (var dir in candidateDirs)
            {
                try
                {
                    var fullDir = Path.GetFullPath(dir);
                    if (Directory.Exists(fullDir))
                    {
                        var files = Directory.GetFiles(fullDir, "*.*", SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            try
                            {
                                File.Delete(file);
                                deletedCount++;
                                _logger.LogInformation("[Storage] Quét dọn tệp rác eKYC: {File}", file);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "[Storage] Không thể xóa tệp: {File}", file);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Storage] Lỗi quét thư mục: {Dir}", dir);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage] Lỗi khi dọn dẹp thư mục tạm {Folder}", folder);
        }

        return Task.FromResult(deletedCount);
    }
}
