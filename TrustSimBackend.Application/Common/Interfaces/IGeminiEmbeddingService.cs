namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Giao diện dịch vụ Vector Embedding kết nối Google Gemini API (text-embedding-004) phục vụ RAG.
/// </summary>
public interface IGeminiEmbeddingService
{
    /// <summary>
    /// Sinh vector nhúng 768 chiều cho một chuỗi văn bản.
    /// </summary>
    Task<ReadOnlyMemory<float>?> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sinh danh sách vector nhúng theo batch cho nhiều đoạn văn bản.
    /// </summary>
    Task<IReadOnlyList<ReadOnlyMemory<float>>> GetEmbeddingsBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}
