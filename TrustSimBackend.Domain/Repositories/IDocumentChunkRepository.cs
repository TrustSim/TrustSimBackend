using Pgvector;
using TrustSimBackend.Domain.Entities.Ai;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý các đoạn văn bản nhúng Vector (DocumentChunk) cho RAG
/// </summary>
public interface IDocumentChunkRepository
{
    Task<DocumentChunk?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentChunk>> GetByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default);
    Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm kiếm Top-K chunks có độ tương đồng Cosine cao nhất với vector truy vấn.
    /// </summary>
    Task<IReadOnlyList<(DocumentChunk Chunk, double Similarity)>> SearchSimilarChunksAsync(
        Vector queryEmbedding,
        int topK = 3,
        double minSimilarity = 0.5,
        CancellationToken cancellationToken = default);
}
