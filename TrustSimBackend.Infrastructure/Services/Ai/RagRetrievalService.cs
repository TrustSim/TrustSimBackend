using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Services.Ai;

public class RagRetrievalService : IRagRetrievalService
{
    private readonly IGeminiEmbeddingService _embeddingService;
    private readonly IDocumentChunkRepository _chunkRepository;
    private readonly ILogger<RagRetrievalService> _logger;
    private readonly IAiCacheService? _aiCache;
    private readonly GeminiSettings _settings;

    public RagRetrievalService(
        IGeminiEmbeddingService embeddingService,
        IDocumentChunkRepository chunkRepository,
        ILogger<RagRetrievalService> logger,
        IAiCacheService? aiCache = null,
        IOptions<GeminiSettings>? settings = null)
    {
        _embeddingService = embeddingService;
        _chunkRepository = chunkRepository;
        _logger = logger;
        _aiCache = aiCache;
        _settings = settings?.Value ?? new GeminiSettings();
    }

    public async Task<IReadOnlyList<DocumentChunkSearchResult>> RetrieveRelevantChunksAsync(
        string query,
        int topK = 3,
        double minSimilarity = 0.3,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<DocumentChunkSearchResult>();
        }

        try
        {
            var queryEmbedding = await GetOrComputeEmbeddingAsync(query, cancellationToken);
            if (!queryEmbedding.HasValue)
            {
                return Array.Empty<DocumentChunkSearchResult>();
            }

            var pgVector = new Vector(queryEmbedding.Value.ToArray());
            var matches = await _chunkRepository.SearchSimilarChunksAsync(pgVector, topK, minSimilarity, cancellationToken);

            var results = matches.Select(m => new DocumentChunkSearchResult
            {
                ChunkId = m.Chunk.Id,
                DocumentId = m.Chunk.DocumentId,
                DocumentName = m.Chunk.Document?.DocumentName ?? "Tài liệu hệ thống",
                Content = m.Chunk.Content,
                Similarity = m.Similarity,
                MetadataJson = m.Chunk.MetadataJson
            }).ToList();

            _logger.LogInformation("RAG Semantic Search tìm thấy {Count} chunks tương đồng phù hợp.", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi trong quá trình RAG Semantic Search: {Message}", ex.Message);
            return Array.Empty<DocumentChunkSearchResult>();
        }
    }

    private async Task<ReadOnlyMemory<float>?> GetOrComputeEmbeddingAsync(string query, CancellationToken cancellationToken)
    {
        var dim = _settings.EmbeddingDimension > 0 ? _settings.EmbeddingDimension : 768;
        var model = string.IsNullOrWhiteSpace(_settings.EmbeddingModel) ? "text-embedding-004" : _settings.EmbeddingModel;

        if (_aiCache != null)
        {
            var cacheKey = _aiCache.BuildHashKey("emb", model, dim.ToString(), query);
            var (found, cachedVector, cacheLayer) = await _aiCache.GetAsync<float[]>(cacheKey, cancellationToken);
            if (found && cachedVector is { Length: > 0 })
            {
                _logger.LogDebug("[RAG Cache HIT - {Layer}] Tái sử dụng vector nhúng cho truy vấn '{Query}'", cacheLayer, query);
                return new ReadOnlyMemory<float>(cachedVector);
            }

            _logger.LogInformation("Sinh vector truy vấn cho RAG: '{Query}'", query);
            var computed = await _embeddingService.GetEmbeddingAsync(query, cancellationToken);
            if (computed.HasValue)
            {
                var ttlMinutes = _settings.EmbeddingCacheTtlMinutes > 0 ? _settings.EmbeddingCacheTtlMinutes : 120;
                await _aiCache.SetAsync(cacheKey, computed.Value.ToArray(), TimeSpan.FromMinutes(ttlMinutes), cancellationToken);
            }
            return computed;
        }

        _logger.LogInformation("Sinh vector truy vấn cho RAG: '{Query}'", query);
        return await _embeddingService.GetEmbeddingAsync(query, cancellationToken);
    }
}
