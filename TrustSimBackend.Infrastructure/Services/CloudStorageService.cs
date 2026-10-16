using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Triển khai dịch vụ lưu trữ hình ảnh tài liệu eKYC và tệp Hợp đồng PDF (hỗ trợ Supabase Storage & Local Fallback).
/// </summary>
public class CloudStorageService : ICloudStorageService
{
    private readonly ILogger<CloudStorageService> _logger;
    private readonly HttpClient _httpClient;
    private readonly SupabaseSettings _supabaseSettings;
    private readonly string _baseStoragePath;

    public CloudStorageService(
        ILogger<CloudStorageService> logger,
        HttpClient httpClient,
        IOptions<SupabaseSettings> supabaseOptions)
    {
        _logger = logger;
        _httpClient = httpClient;
        _supabaseSettings = supabaseOptions.Value;
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
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("Tệp tải lên không hợp lệ hoặc rỗng.", nameof(file));
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "image/jpeg" : file.ContentType;

        // Tối ưu hóa: Stream trực tiếp từ file thay vì cấp phát MemoryStream gây tốn RAM và GC
        await using var stream = file.OpenReadStream();
        return await UploadStreamToSupabaseAsync(stream, fileName, folder, contentType, cancellationToken);
    }

    public async Task<string> UploadImageBytesAsync(byte[] imageBytes, string fileName, string folder, string contentType = "image/jpeg", CancellationToken cancellationToken = default)
    {
        var cleanFileName = SanitizeFileName(fileName);

        // Luôn tải trực tiếp 100% lên Cloud Supabase Storage, không lưu ổ cứng BE
        if (!string.IsNullOrWhiteSpace(_supabaseSettings.Url) && !string.IsNullOrWhiteSpace(_supabaseSettings.AnonKey))
        {
            var supabasePublicUrl = await UploadToSupabaseAsync(imageBytes, cleanFileName, folder, contentType, cancellationToken);
            if (!string.IsNullOrWhiteSpace(supabasePublicUrl))
            {
                _logger.LogInformation("[Storage] ✅ Đã tải tệp lên Supabase Storage thành công: {Url}", supabasePublicUrl);
                return supabasePublicUrl;
            }
        }

        throw new InvalidOperationException("Chưa cấu hình Supabase Storage hoặc thông tin Supabase không hợp lệ. Hệ thống được cấu hình không lưu trữ tệp trên máy chủ nội bộ.");
    }

    private async Task<string> UploadStreamToSupabaseAsync(
        Stream stream,
        string fileName,
        string folder,
        string contentType,
        CancellationToken cancellationToken)
    {
        var cleanFileName = SanitizeFileName(fileName);
        var baseUrl = _supabaseSettings.Url.TrimEnd('/');
        var bucket = string.IsNullOrWhiteSpace(_supabaseSettings.Bucket) ? "pdf-files" : _supabaseSettings.Bucket;

        var objectPath = string.IsNullOrWhiteSpace(folder) ? cleanFileName : $"{folder.Trim('/')}/{cleanFileName}";
        var uploadUrl = $"{baseUrl}/storage/v1/object/{bucket}/{objectPath}";

        using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        request.Headers.Add("apikey", _supabaseSettings.AnonKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseSettings.AnonKey);
        request.Headers.Add("x-upsert", "true");

        using var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        request.Content = streamContent;

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var publicUrl = $"{baseUrl}/storage/v1/object/public/{bucket}/{objectPath}";
            _logger.LogInformation("[Storage] ✅ Đã stream tệp lên Supabase Storage thành công: {Url}", publicUrl);
            return publicUrl;
        }

        var errorMsg = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError("[Storage] ❌ Supabase Storage phản hồi mã lỗi {StatusCode}: {Error}.", response.StatusCode, errorMsg);
        throw new InvalidOperationException($"Không thể tải tệp lên Supabase Storage ({response.StatusCode}): {errorMsg}");
    }

    private async Task<string?> UploadToSupabaseAsync(
        byte[] fileBytes,
        string fileName,
        string folder,
        string contentType,
        CancellationToken cancellationToken)
    {
        var baseUrl = _supabaseSettings.Url.TrimEnd('/');
        var bucket = string.IsNullOrWhiteSpace(_supabaseSettings.Bucket) ? "pdf-files" : _supabaseSettings.Bucket;

        var objectPath = string.IsNullOrWhiteSpace(folder) ? fileName : $"{folder.Trim('/')}/{fileName}";
        var uploadUrl = $"{baseUrl}/storage/v1/object/{bucket}/{objectPath}";

        using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        request.Headers.Add("apikey", _supabaseSettings.AnonKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseSettings.AnonKey);
        request.Headers.Add("x-upsert", "true");

        var byteContent = new ByteArrayContent(fileBytes);
        byteContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        request.Content = byteContent;

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return $"{baseUrl}/storage/v1/object/public/{bucket}/{objectPath}";
        }

        var errorMsg = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError("[Storage] ❌ Supabase Storage phản hồi mã lỗi {StatusCode}: {Error}.", response.StatusCode, errorMsg);
        throw new InvalidOperationException($"Không thể tải tệp lên Supabase Storage ({response.StatusCode}): {errorMsg}");
    }

    private static string SanitizeFileName(string fileName, string defaultExt = ".jpg")
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return $"{Guid.NewGuid():N}{defaultExt}";
        }

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = defaultExt;

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleanChars = baseName.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray();
        var clean = new string(cleanChars).Trim();

        return string.IsNullOrWhiteSpace(clean) ? $"{Guid.NewGuid():N}{ext}" : $"{clean}{ext}";
    }

    public async Task<bool> DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileUrl)) return false;

            // 1. Kiểm tra và xử lý xóa trên Cloud Supabase Storage
            var baseUrl = _supabaseSettings.Url.TrimEnd('/');
            var publicPrefix = $"{baseUrl}/storage/v1/object/public/";

            if (fileUrl.StartsWith(publicPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var remaining = fileUrl.Substring(publicPrefix.Length);
                var slashIndex = remaining.IndexOf('/');
                if (slashIndex > 0)
                {
                    var bucket = remaining.Substring(0, slashIndex);
                    var objectPath = remaining.Substring(slashIndex + 1);

                    var deleteUrl = $"{baseUrl}/storage/v1/object/{bucket}/{objectPath}";

                    using var request = new HttpRequestMessage(HttpMethod.Delete, deleteUrl);
                    request.Headers.Add("apikey", _supabaseSettings.AnonKey);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseSettings.AnonKey);

                    var response = await _httpClient.SendAsync(request, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("[Storage] ✅ Đã xóa tệp trên Supabase Storage thành công: {ObjectPath}", objectPath);
                        return true;
                    }

                    _logger.LogWarning("[Storage] ⚠️ Supabase Storage phản hồi không thể xóa ({StatusCode}): {Url}", response.StatusCode, fileUrl);
                    return false;
                }
            }

            // 2. Fallback xóa tệp cục bộ nếu là file local cũ
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

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage] Lỗi khi xử lý xóa tệp eKYC: {Url}", fileUrl);
            return false;
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
