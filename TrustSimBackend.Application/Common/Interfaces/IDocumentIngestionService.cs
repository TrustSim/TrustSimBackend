namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ xử lý nạp và đánh chỉ mục tài liệu (Document Ingestion & RAG Indexing Pipeline)
/// </summary>
public interface IDocumentIngestionService
{
    /// <summary>
    /// Xử lý cào dữ liệu, chia nhỏ chunks, sinh vector embeddings và lưu vào DB
    /// </summary>
    Task IngestDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
}
