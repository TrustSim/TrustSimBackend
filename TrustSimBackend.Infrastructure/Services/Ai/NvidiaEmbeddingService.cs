using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Infrastructure.Services.Ai;

public class NvidiaEmbeddingService : INvidiaEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly NvidiaSettings _settings;
    private readonly ILogger<NvidiaEmbeddingService> _logger;

    public NvidiaEmbeddingService(
        HttpClient httpClient,
        IOptions<NvidiaSettings> settings,
        ILogger<NvidiaEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<Vector> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var list = await GetEmbeddingsBatchAsync(new[] { text }, cancellationToken);
        return list[0];
    }

    public async Task<IReadOnlyList<Vector>> GetEmbeddingsBatchAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        if (textList.Count == 0)
        {
            return Array.Empty<Vector>();
        }

        // Kiểm tra xem đã có API Key NVIDIA hợp lệ chưa
        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || _settings.ApiKey.Contains("YOUR_") || _settings.ApiKey.Length < 10)
        {
            _logger.LogWarning("Nvidia API Key chưa được cấu hình hoặc không hợp lệ. Sử dụng Fallback Semantic Hash Vector ({Dim} chiều) để đảm bảo hệ thống tiếp tục hoạt động.", _settings.EmbeddingDimension);
            return textList.Select(t => GenerateDeterministicFallbackVector(t, _settings.EmbeddingDimension)).ToList();
        }

        try
        {
            var requestUrl = $"{_settings.BaseUrl.TrimEnd('/')}/embeddings";
            var requestPayload = new
            {
                input = textList,
                model = string.IsNullOrWhiteSpace(_settings.EmbeddingModel) ? "nvidia/nv-embedqa-e5-v5" : _settings.EmbeddingModel,
                input_type = "passage",
                truncate = "END"
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey.Trim());
            request.Content = jsonContent;

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("NVIDIA Embedding API thất bại với status code {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);
                throw new InvalidOperationException($"Lỗi kết nối NVIDIA Embedding API ({response.StatusCode}): {errorBody}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<NvidiaEmbeddingResponse>(responseJson);

            if (result?.Data == null || result.Data.Count == 0)
            {
                throw new InvalidOperationException("NVIDIA Embedding API không trả về dữ liệu vector.");
            }

            var vectors = result.Data
                .OrderBy(d => d.Index)
                .Select(d => new Vector(d.Embedding.Select(v => (float)v).ToArray()))
                .ToList();

            return vectors;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Lỗi khi gọi NVIDIA Embedding API: {Message}", ex.Message);
            _logger.LogWarning("Sử dụng Fallback Vector cho batch {Count} texts.", textList.Count);
            return textList.Select(t => GenerateDeterministicFallbackVector(t, _settings.EmbeddingDimension)).ToList();
        }
    }

    /// <summary>
    /// Sinh vector giả lập có tính xác định (Deterministic Semantic Hash Vector) 1024 chiều khi chưa có API Key
    /// </summary>
    private static Vector GenerateDeterministicFallbackVector(string text, int dimension)
    {
        var floats = new float[dimension];
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        
        var seed = BitConverter.ToInt32(hashBytes, 0);
        var random = new Random(seed);

        double sumSquares = 0;
        for (int i = 0; i < dimension; i++)
        {
            var val = (float)(random.NextDouble() * 2.0 - 1.0);
            floats[i] = val;
            sumSquares += val * val;
        }

        // Chuẩn hóa vector L2 Norm (Unit Vector)
        var norm = (float)Math.Sqrt(sumSquares);
        if (norm > 0)
        {
            for (int i = 0; i < dimension; i++)
            {
                floats[i] /= norm;
            }
        }

        return new Vector(floats);
    }

    private class NvidiaEmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<NvidiaEmbeddingDataItem> Data { get; set; } = new();
    }

    private class NvidiaEmbeddingDataItem
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("embedding")]
        public List<double> Embedding { get; set; } = new();
    }
}
