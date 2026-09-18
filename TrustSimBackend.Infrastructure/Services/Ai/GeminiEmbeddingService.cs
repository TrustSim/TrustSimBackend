using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Infrastructure.Services.Ai;

/// <summary>
/// Dịch vụ kết nối Google Gemini Embedding API (text-embedding-004) để sinh vector nhúng cho RAG.
/// </summary>
public class GeminiEmbeddingService : IGeminiEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiEmbeddingService> _logger;

    public GeminiEmbeddingService(
        HttpClient httpClient,
        IOptions<GeminiSettings> settings,
        ILogger<GeminiEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ReadOnlyMemory<float>?> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var list = await GetEmbeddingsBatchAsync(new[] { text }, cancellationToken);
        return list.Count > 0 ? list[0] : null;
    }

    public async Task<IReadOnlyList<ReadOnlyMemory<float>>> GetEmbeddingsBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (texts == null || texts.Count == 0)
        {
            return Array.Empty<ReadOnlyMemory<float>>();
        }

        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || _settings.ApiKey.Length < 10)
        {
            _logger.LogWarning("Gemini API Key chưa được cấu hình. Sử dụng embedding giả lập cục bộ.");
            return GenerateDeterministicMockEmbeddings(texts);
        }

        var targetDim = _settings.EmbeddingDimension > 0 ? _settings.EmbeddingDimension : 2048;

        try
        {
            var requestUrl = $"{_settings.BaseUrl.TrimEnd('/')}/embeddings";
            var requestPayload = new
            {
                model = string.IsNullOrWhiteSpace(_settings.EmbeddingModel) ? "gemini-embedding-001" : _settings.EmbeddingModel,
                input = texts.Count == 1 ? (object)texts[0] : texts,
                dimensions = targetDim
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey.Trim());
            request.Content = jsonContent;

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Gemini Embedding API thất bại ({StatusCode}): {ErrorBody}", response.StatusCode, errorBody);
                return GenerateDeterministicMockEmbeddings(texts);
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<GeminiEmbeddingApiResponse>(responseJson);

            if (result?.Data != null && result.Data.Count > 0)
            {
                return result.Data.Select(d =>
                {
                    var raw = d.Embedding.ToArray();
                    if (raw.Length == targetDim)
                    {
                        return new ReadOnlyMemory<float>(raw);
                    }

                    var adjusted = new float[targetDim];
                    Array.Copy(raw, adjusted, Math.Min(raw.Length, targetDim));
                    double norm = Math.Sqrt(adjusted.Sum(v => v * v));
                    if (norm > 0)
                    {
                        for (int i = 0; i < targetDim; i++)
                        {
                            adjusted[i] = (float)(adjusted[i] / norm);
                        }
                    }
                    return new ReadOnlyMemory<float>(adjusted);
                }).ToList();
            }

            return GenerateDeterministicMockEmbeddings(texts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi gọi Gemini Embedding API: {Message}", ex.Message);
            return GenerateDeterministicMockEmbeddings(texts);
        }
    }

    private IReadOnlyList<ReadOnlyMemory<float>> GenerateDeterministicMockEmbeddings(IReadOnlyList<string> texts)
    {
        var dim = _settings.EmbeddingDimension > 0 ? _settings.EmbeddingDimension : 2048;
        var results = new List<ReadOnlyMemory<float>>();

        foreach (var text in texts)
        {
            var vector = new float[dim];
            int seed = Math.Abs(text.GetHashCode());
            var rnd = new Random(seed);

            for (int i = 0; i < dim; i++)
            {
                vector[i] = (float)(rnd.NextDouble() * 2 - 1);
            }

            // Normalize vector L2 norm
            double norm = Math.Sqrt(vector.Sum(v => v * v));
            if (norm > 0)
            {
                for (int i = 0; i < dim; i++)
                {
                    vector[i] = (float)(vector[i] / norm);
                }
            }

            results.Add(new ReadOnlyMemory<float>(vector));
        }

        return results;
    }

    private class GeminiEmbeddingApiResponse
    {
        [JsonPropertyName("data")]
        public List<GeminiEmbeddingDataItem> Data { get; set; } = new();
    }

    private class GeminiEmbeddingDataItem
    {
        [JsonPropertyName("embedding")]
        public List<float> Embedding { get; set; } = new();
    }
}
