using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ RAG Retrieval thực hiện Semantic Vector Search trên cơ sở dữ liệu
/// </summary>
public interface IRagRetrievalService
{
    /// <summary>
    /// Tìm kiếm Top-K chunks liên quan nhất với câu truy vấn ngữ nghĩa
    /// </summary>
    Task<IReadOnlyList<DocumentChunkSearchResult>> RetrieveRelevantChunksAsync(
        string query,
        int topK = 3,
        double minSimilarity = 0.4,
        CancellationToken cancellationToken = default);
}
