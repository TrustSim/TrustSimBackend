using Microsoft.Extensions.Logging;
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

    public RagRetrievalService(
        IGeminiEmbeddingService embeddingService,
        IDocumentChunkRepository chunkRepository,
        ILogger<RagRetrievalService> logger)
    {
        _embeddingService = embeddingService;
        _chunkRepository = chunkRepository;
        _logger = logger;
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
            _logger.LogInformation("Sinh vector truy vấn cho RAG: '{Query}'", query);
            var queryEmbedding = await _embeddingService.GetEmbeddingAsync(query, cancellationToken);
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
}
