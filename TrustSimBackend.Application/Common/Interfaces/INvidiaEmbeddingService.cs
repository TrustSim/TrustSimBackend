using Pgvector;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ kết nối và sinh Vector Embeddings từ NVIDIA Embedding API (nv-embedqa-e5-v5 / baai/bge-m3)
/// </summary>
public interface INvidiaEmbeddingService
{
    /// <summary>
    /// Sinh vector nhúng (1024 chiều) cho một đoạn văn bản
    /// </summary>
    Task<Vector> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sinh vector nhúng theo lô (Batch) cho nhiều đoạn văn bản
    /// </summary>
    Task<IReadOnlyList<Vector>> GetEmbeddingsBatchAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);
}
