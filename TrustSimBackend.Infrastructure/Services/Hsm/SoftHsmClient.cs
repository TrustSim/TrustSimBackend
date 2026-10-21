using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Infrastructure.Services.Hsm;

/// <summary>
/// Client kết nối HTTP với Universal SoftHSMv2 Microservice với cơ chế Retry, Connection Pooling và Cache Public Key.
/// </summary>
public class SoftHsmClient : ISoftHsmClient
{
    private readonly HttpClient _httpClient;
    private readonly SoftHsmSettings _settings;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<SoftHsmClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SoftHsmClient(
        HttpClient httpClient,
        IOptions<SoftHsmSettings> settings,
        IMemoryCache memoryCache,
        ILogger<SoftHsmClient> logger)
    {
        _settings = settings.Value;
        _memoryCache = memoryCache;
        _logger = logger;

        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds > 0 ? _settings.TimeoutSeconds : 60);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, object? body = null)
    {
        var req = new HttpRequestMessage(method, path);
        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            req.Headers.Add("X-API-Key", _settings.ApiKey);
        }

        if (body != null)
        {
            req.Content = JsonContent.Create(body);
        }

        return req;
    }

    private async Task<T> SendWithRetryAsync<T>(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Max(1, _settings.RetryCount);
        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var request = requestFactory();
                using var response = await _httpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("[SoftHSM Client] HTTP {StatusCode} từ SoftHSM ({Attempt}/{Max}): {Body}",
                        response.StatusCode, attempt, maxAttempts, errorBody);

                    // Nếu lỗi do Client (400, 401, 404, 409, 422), không retry vô ích
                    if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                    {
                        throw new HttpRequestException($"SoftHSM returned {(int)response.StatusCode}: {errorBody}", null, response.StatusCode);
                    }

                    response.EnsureSuccessStatusCode();
                }

                var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
                if (result == null)
                {
                    throw new InvalidOperationException("Phản hồi từ SoftHSM là null.");
                }

                return result;
            }
            catch (HttpRequestException ex) when (ex.StatusCode.HasValue && (int)ex.StatusCode.Value < 500)
            {
                // Không retry với lỗi 4xx
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "[SoftHSM Client] Lỗi kết nối SoftHSM lần {Attempt}/{Max}: {Message}",
                    attempt, maxAttempts, ex.Message);

                if (attempt < maxAttempts)
                {
                    var delayMs = (int)(Math.Pow(2, attempt - 1) * 500);
                    await Task.Delay(delayMs, cancellationToken);
                }
            }
        }

        throw new InvalidOperationException($"Không thể hoàn tất gọi SoftHSM sau {maxAttempts} lần thử.", lastException);
    }

    public async Task<HsmHealthResponse> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        return await SendWithRetryAsync<HsmHealthResponse>(() => new HttpRequestMessage(HttpMethod.Get, "health"), cancellationToken);
    }

    public async Task<HsmGenerateKeyResponse> GenerateKeyPairAsync(HsmGenerateKeyRequest request, CancellationToken cancellationToken = default)
    {
        var res = await SendWithRetryAsync<HsmGenerateKeyResponse>(
            () => CreateRequest(HttpMethod.Post, "api/v1/hsm/generate-keypair", request), cancellationToken);

        // Nạp ngay Public Key vừa sinh vào cache
        if (!string.IsNullOrWhiteSpace(res.PublicKeyPem))
        {
            var cacheKey = $"HSM_PUBKEY_{res.KeyLabel}";
            _memoryCache.Set(cacheKey, res.PublicKeyPem, TimeSpan.FromHours(24));
        }

        return res;
    }

    public async Task<HsmSignResponse> SignAsync(HsmSignRequest request, CancellationToken cancellationToken = default)
    {
        return await SendWithRetryAsync<HsmSignResponse>(
            () => CreateRequest(HttpMethod.Post, "api/v1/hsm/sign", request), cancellationToken);
    }

    public async Task<HsmVerifyResponse> VerifyAsync(HsmVerifyRequest request, CancellationToken cancellationToken = default)
    {
        return await SendWithRetryAsync<HsmVerifyResponse>(
            () => CreateRequest(HttpMethod.Post, "api/v1/hsm/verify", request), cancellationToken);
    }

    public async Task<HsmEncryptResponse> EncryptAsync(HsmEncryptRequest request, CancellationToken cancellationToken = default)
    {
        return await SendWithRetryAsync<HsmEncryptResponse>(
            () => CreateRequest(HttpMethod.Post, "api/v1/hsm/encrypt", request), cancellationToken);
    }

    public async Task<HsmDecryptResponse> DecryptAsync(HsmDecryptRequest request, CancellationToken cancellationToken = default)
    {
        return await SendWithRetryAsync<HsmDecryptResponse>(
            () => CreateRequest(HttpMethod.Post, "api/v1/hsm/decrypt", request), cancellationToken);
    }

    public async Task<string> GetPublicKeyPemAsync(string keyLabel, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"HSM_PUBKEY_{keyLabel}";
        if (_memoryCache.TryGetValue(cacheKey, out string? cachedPem) && !string.IsNullOrWhiteSpace(cachedPem))
        {
            return cachedPem;
        }

        var path = $"api/v1/hsm/public-key/{Uri.EscapeDataString(keyLabel)}";
        var res = await SendWithRetryAsync<HsmPublicKeyResponse>(
            () => CreateRequest(HttpMethod.Get, path), cancellationToken);

        if (!string.IsNullOrWhiteSpace(res.PublicKeyPem))
        {
            _memoryCache.Set(cacheKey, res.PublicKeyPem, TimeSpan.FromHours(24));
        }

        return res.PublicKeyPem;
    }

    public async Task<HsmBackupResponse> BackupAsync(CancellationToken cancellationToken = default)
    {
        return await SendWithRetryAsync<HsmBackupResponse>(
            () => CreateRequest(HttpMethod.Post, "api/v1/hsm/backup"), cancellationToken);
    }

    public async Task<HsmBackupResponse> RestoreAsync(CancellationToken cancellationToken = default)
    {
        return await SendWithRetryAsync<HsmBackupResponse>(
            () => CreateRequest(HttpMethod.Post, "api/v1/hsm/restore"), cancellationToken);
    }
}
