namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ xử lý nạp và đánh chỉ mục tài liệu (Document Ingestion & RAG Indexing Pipeline)
/// </summary>
public interface IDocumentIngestionService
{
    /// <summary>
    /// Xử lý cào dữ liệu từ URL, chia nhỏ chunks, sinh vector embeddings và lưu vào DB
    /// </summary>
    Task IngestDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xử lý nạp văn bản từ một Stream tệp tải lên (PDF, TXT, MD, DOCX), chia nhỏ chunks và sinh vector embeddings
    /// </summary>
    Task IngestDocumentFromStreamAsync(Guid documentId, Stream stream, string fileName, CancellationToken cancellationToken = default);
}
