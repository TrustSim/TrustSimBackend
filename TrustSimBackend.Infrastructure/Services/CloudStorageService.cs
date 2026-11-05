using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Triển khai dịch vụ lưu trữ hình ảnh tài liệu eKYC và tệp Hợp đồng PDF qua MinIO / AWS S3 Compatible Storage.
/// </summary>
public class CloudStorageService : ICloudStorageService
{
    private static bool _bucketInitialized;
    private static readonly SemaphoreSlim _initLock = new(1, 1);

    private readonly ILogger<CloudStorageService> _logger;
    private readonly IAmazonS3 _s3Client;
    private readonly MinioSettings _minioSettings;

    public CloudStorageService(
        ILogger<CloudStorageService> logger,
        IAmazonS3 s3Client,
        IOptions<MinioSettings> minioOptions)
    {
        _logger = logger;
        _s3Client = s3Client;
        _minioSettings = minioOptions.Value;
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

        await using var stream = file.OpenReadStream();
        return await UploadStreamToS3Async(stream, fileName, folder, contentType, cancellationToken);
    }

    public async Task<string> UploadImageBytesAsync(
        byte[] imageBytes, 
        string fileName, 
        string folder, 
        string contentType = "image/jpeg", 
        CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(imageBytes);
        return await UploadStreamToS3Async(stream, fileName, folder, contentType, cancellationToken);
    }

    private async Task<string> UploadStreamToS3Async(
        Stream stream,
        string fileName,
        string folder,
        string contentType,
        CancellationToken cancellationToken)
    {
        var cleanFileName = SanitizeFileName(fileName);
        var bucket = string.IsNullOrWhiteSpace(_minioSettings.Bucket) ? "pdf-files" : _minioSettings.Bucket;
        var objectKey = string.IsNullOrWhiteSpace(folder) ? cleanFileName : $"{folder.Trim('/')}/{cleanFileName}";

        await EnsureBucketReadyAsync(bucket, cancellationToken);

        try
        {
            var putRequest = new PutObjectRequest
            {
                BucketName = bucket,
                Key = objectKey,
                InputStream = stream,
                ContentType = contentType
            };

            await _s3Client.PutObjectAsync(putRequest, cancellationToken);

            var publicBase = _minioSettings.PublicUrl.TrimEnd('/');
            var publicUrl = $"{publicBase}/{bucket}/{objectKey}";
            _logger.LogInformation("[MinIO Storage] ✅ Đã tải tệp lên S3 Storage thành công: {Url}", publicUrl);
            return publicUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MinIO Storage] ❌ Không thể tải tệp lên MinIO S3 Bucket '{Bucket}', Key '{Key}'", bucket, objectKey);
            throw;
        }
    }

    private async Task EnsureBucketReadyAsync(string bucket, CancellationToken cancellationToken)
    {
        if (_bucketInitialized) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_bucketInitialized) return;

            var exists = await AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucket);
            if (!exists)
            {
                await _s3Client.PutBucketAsync(new PutBucketRequest { BucketName = bucket }, cancellationToken);
                _logger.LogInformation("[MinIO Storage] ✅ Đã tự động tạo Bucket '{Bucket}'", bucket);

                var policy = $@"{{
  ""Version"": ""2012-10-17"",
  ""Statement"": [
    {{
      ""Effect"": ""Allow"",
      ""Principal"": ""*"",
      ""Action"": [""s3:GetObject""],
      ""Resource"": [""arn:aws:s3:::{bucket}/*""]
    }}
  ]
}}";
                await _s3Client.PutBucketPolicyAsync(new PutBucketPolicyRequest
                {
                    BucketName = bucket,
                    Policy = policy
                }, cancellationToken);
                _logger.LogInformation("[MinIO Storage] ✅ Đã cấu hình Public Read Policy cho Bucket '{Bucket}'", bucket);
            }

            _bucketInitialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MinIO Storage] Cảnh báo khi kiểm tra/tạo bucket '{Bucket}'", bucket);
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<bool> DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl)) return false;

        try
        {
            var bucket = string.IsNullOrWhiteSpace(_minioSettings.Bucket) ? "pdf-files" : _minioSettings.Bucket;
            var objectKey = ExtractObjectKey(fileUrl, bucket);
            if (string.IsNullOrWhiteSpace(objectKey)) return false;

            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = bucket,
                Key = objectKey
            };

            await _s3Client.DeleteObjectAsync(deleteRequest, cancellationToken);
            _logger.LogInformation("[MinIO Storage] ✅ Đã xóa tệp trên MinIO thành công: {Key}", objectKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MinIO Storage] ⚠️ Lỗi khi xóa tệp MinIO: {Url}", fileUrl);
            return false;
        }
    }

    public Task<int> CleanAllTempUploadsAsync(string folder = "kyc", CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }

    private string? ExtractObjectKey(string fileUrl, string bucket)
    {
        var prefix = $"/{bucket}/";
        var idx = fileUrl.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            return fileUrl.Substring(idx + prefix.Length);
        }

        return Path.GetFileName(fileUrl);
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
}
