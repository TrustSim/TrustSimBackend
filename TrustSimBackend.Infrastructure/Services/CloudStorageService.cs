using System.Collections.Concurrent;
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
/// Triển khai dịch vụ lưu trữ hình ảnh tài liệu eKYC, ảnh Chat và tệp Hợp đồng PDF qua MinIO / AWS S3 Compatible Storage.
/// </summary>
public class CloudStorageService : ICloudStorageService
{
    private static readonly ConcurrentDictionary<string, bool> _initializedBuckets = new(StringComparer.OrdinalIgnoreCase);
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
        var bucket = string.IsNullOrWhiteSpace(_minioSettings.Bucket) ? "pdf-files" : _minioSettings.Bucket;

        await using var stream = file.OpenReadStream();
        return await UploadStreamToS3Async(stream, bucket, fileName, folder, contentType, expirationDays: null, cancellationToken);
    }

    public async Task<string> UploadImageBytesAsync(
        byte[] imageBytes,
        string fileName,
        string folder,
        string contentType = "image/jpeg",
        CancellationToken cancellationToken = default)
    {
        var bucket = string.IsNullOrWhiteSpace(_minioSettings.Bucket) ? "pdf-files" : _minioSettings.Bucket;
        using var stream = new MemoryStream(imageBytes);
        return await UploadStreamToS3Async(stream, bucket, fileName, folder, contentType, expirationDays: null, cancellationToken);
    }

    public async Task<string> UploadChatImageAsync(
        IFormFile file,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("Tệp hình ảnh tải lên không hợp lệ hoặc rỗng.", nameof(file));
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";
        var fileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "image/jpeg" : file.ContentType;

        var chatBucket = string.IsNullOrWhiteSpace(_minioSettings.ChatImageBucket)
            ? "chat-images"
            : _minioSettings.ChatImageBucket.Trim();

        var expirationDays = _minioSettings.ChatImageExpirationDays > 0
            ? _minioSettings.ChatImageExpirationDays
            : 7;

        var folder = $"conversations/{conversationId:N}";

        await using var stream = file.OpenReadStream();
        return await UploadStreamToS3Async(
            stream,
            chatBucket,
            fileName,
            folder,
            contentType,
            expirationDays: expirationDays,
            cancellationToken);
    }

    private async Task<string> UploadStreamToS3Async(
        Stream stream,
        string bucket,
        string fileName,
        string folder,
        string contentType,
        int? expirationDays,
        CancellationToken cancellationToken)
    {
        var cleanFileName = SanitizeFileName(fileName);
        var objectKey = string.IsNullOrWhiteSpace(folder) ? cleanFileName : $"{folder.Trim('/')}/{cleanFileName}";

        await EnsureBucketReadyAsync(bucket, expirationDays, cancellationToken);

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

    private async Task EnsureBucketReadyAsync(string bucket, int? expirationDays, CancellationToken cancellationToken)
    {
        var cacheKey = $"{bucket}:{expirationDays ?? 0}";
        if (_initializedBuckets.ContainsKey(cacheKey)) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initializedBuckets.ContainsKey(cacheKey)) return;

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

            if (expirationDays.HasValue && expirationDays.Value > 0)
            {
                try
                {
                    var lifecycleRequest = new PutLifecycleConfigurationRequest
                    {
                        BucketName = bucket,
                        Configuration = new LifecycleConfiguration
                        {
                            Rules = new List<LifecycleRule>
                            {
                                new LifecycleRule
                                {
                                    Id = $"auto-expire-{expirationDays.Value}-days",
                                    Status = LifecycleRuleStatus.Enabled,
                                    Filter = new LifecycleFilter
                                    {
                                        LifecycleFilterPredicate = new LifecyclePrefixPredicate { Prefix = string.Empty }
                                    },
                                    Expiration = new LifecycleRuleExpiration
                                    {
                                        Days = expirationDays.Value
                                    }
                                }
                            }
                        }
                    };

                    await _s3Client.PutLifecycleConfigurationAsync(lifecycleRequest, cancellationToken);
                    _logger.LogInformation(
                        "[MinIO Storage] ✅ Đã cấu hình Lifecycle tự động xóa sau {Days} ngày cho Bucket '{Bucket}'",
                        expirationDays.Value, bucket);
                }
                catch (Exception lcEx)
                {
                    _logger.LogWarning(
                        lcEx,
                        "[MinIO Storage] Cảnh báo khi thiết lập Lifecycle cho Bucket '{Bucket}' (sẽ dùng Background Cleanup Worker bổ trợ).",
                        bucket);
                }
            }

            _initializedBuckets[cacheKey] = true;
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

    public async Task<int> CleanupExpiredChatImagesAsync(CancellationToken cancellationToken = default)
    {
        var chatBucket = string.IsNullOrWhiteSpace(_minioSettings.ChatImageBucket)
            ? "chat-images"
            : _minioSettings.ChatImageBucket.Trim();

        var retentionDays = _minioSettings.ChatImageExpirationDays > 0
            ? _minioSettings.ChatImageExpirationDays
            : 7;

        var cutoffUtc = DateTime.UtcNow.AddDays(-retentionDays);
        int deletedCount = 0;

        try
        {
            var exists = await AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, chatBucket);
            if (!exists) return 0;

            string? continuationToken = null;
            do
            {
                var listResponse = await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = chatBucket,
                    ContinuationToken = continuationToken
                }, cancellationToken);

                foreach (var obj in listResponse.S3Objects)
                {
                    if (obj.LastModified.ToUniversalTime() <= cutoffUtc)
                    {
                        await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
                        {
                            BucketName = chatBucket,
                            Key = obj.Key
                        }, cancellationToken);
                        deletedCount++;
                    }
                }

                continuationToken = listResponse.IsTruncated ? listResponse.NextContinuationToken : null;
            }
            while (!string.IsNullOrEmpty(continuationToken));

            if (deletedCount > 0)
            {
                _logger.LogInformation(
                    "[MinIO Storage] 🧹 Đã tự động xóa giải phóng {Count} ảnh chat quá hạn ({Days} ngày) trong Bucket '{Bucket}'.",
                    deletedCount, retentionDays, chatBucket);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MinIO Storage] Lỗi khi quét dọn dẹp ảnh chat quá hạn trong Bucket '{Bucket}'", chatBucket);
        }

        return deletedCount;
    }

    public async Task<bool> DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl)) return false;

        try
        {
            var defaultBucket = string.IsNullOrWhiteSpace(_minioSettings.Bucket) ? "pdf-files" : _minioSettings.Bucket;
            var chatBucket = string.IsNullOrWhiteSpace(_minioSettings.ChatImageBucket) ? "chat-images" : _minioSettings.ChatImageBucket;

            var (targetBucket, objectKey) = ResolveBucketAndObjectKey(fileUrl, defaultBucket, chatBucket);
            if (string.IsNullOrWhiteSpace(objectKey)) return false;

            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = targetBucket,
                Key = objectKey
            };

            await _s3Client.DeleteObjectAsync(deleteRequest, cancellationToken);
            _logger.LogInformation("[MinIO Storage] ✅ Đã xóa tệp trên MinIO thành công ({Bucket}/{Key})", targetBucket, objectKey);
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

    private static (string Bucket, string? ObjectKey) ResolveBucketAndObjectKey(string fileUrl, string defaultBucket, string chatBucket)
    {
        var chatPrefix = $"/{chatBucket}/";
        var chatIdx = fileUrl.IndexOf(chatPrefix, StringComparison.OrdinalIgnoreCase);
        if (chatIdx >= 0)
        {
            return (chatBucket, fileUrl.Substring(chatIdx + chatPrefix.Length));
        }

        var defaultPrefix = $"/{defaultBucket}/";
        var defaultIdx = fileUrl.IndexOf(defaultPrefix, StringComparison.OrdinalIgnoreCase);
        if (defaultIdx >= 0)
        {
            return (defaultBucket, fileUrl.Substring(defaultIdx + defaultPrefix.Length));
        }

        return (defaultBucket, Path.GetFileName(fileUrl));
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
