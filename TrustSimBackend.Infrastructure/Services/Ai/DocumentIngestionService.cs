using Microsoft.Extensions.Logging;
using Pgvector;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Domain.Entities.Ai;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Services.Ai;

public class DocumentIngestionService : IDocumentIngestionService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentChunkRepository _chunkRepository;
    private readonly IWebCrawlerService _crawlerService;
    private readonly ITextChunkerService _chunkerService;
    private readonly IGeminiEmbeddingService _embeddingService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DocumentIngestionService> _logger;

    public DocumentIngestionService(
        IDocumentRepository documentRepository,
        IDocumentChunkRepository chunkRepository,
        IWebCrawlerService crawlerService,
        ITextChunkerService chunkerService,
        IGeminiEmbeddingService embeddingService,
        IUnitOfWork unitOfWork,
        ILogger<DocumentIngestionService> logger)
    {
        _documentRepository = documentRepository;
        _chunkRepository = chunkRepository;
        _crawlerService = crawlerService;
        _chunkerService = chunkerService;
        _embeddingService = embeddingService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task IngestDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var doc = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
        if (doc == null)
        {
            _logger.LogWarning("Không tìm thấy tài liệu ID {DocumentId} để thực hiện Ingestion.", documentId);
            return;
        }

        try
        {
            _logger.LogInformation("Bắt đầu quy trình RAG Ingestion cho tài liệu: {Name} (URL: {Url})", doc.DocumentName, doc.LinkUrl);

            // Bước 1: Cập nhật trạng thái CRAWLING
            doc.Status = DocumentStatus.CRAWLING;
            doc.ErrorMessage = null;
            _documentRepository.Update(doc);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Bước 2: Tải và làm sạch nội dung từ URL
            var cleanText = await _crawlerService.FetchAndExtractCleanTextAsync(doc.LinkUrl, cancellationToken);
            if (string.IsNullOrWhiteSpace(cleanText))
            {
                throw new InvalidOperationException("Nội dung tải về từ URL rỗng.");
            }

            doc.Status = DocumentStatus.PROCESSING;
            _documentRepository.Update(doc);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Bước 3: Bẻ nhỏ văn bản thành các Chunks
            var chunkItems = _chunkerService.ChunkText(cleanText, chunkSize: 600, overlapSize: 100, documentName: doc.DocumentName);
            if (chunkItems.Count == 0)
            {
                throw new InvalidOperationException("Không thể tạo Chunks từ văn bản.");
            }

            _logger.LogInformation("Tài liệu '{Name}' được chia thành {Count} đoạn chunks.", doc.DocumentName, chunkItems.Count);

            // Bước 4: Xóa các chunks cũ nếu là tài liệu nạp lại
            await _chunkRepository.DeleteByDocumentIdAsync(doc.Id, cancellationToken);

            // Bước 5: Sinh Vector Embeddings theo batch
            var chunkTexts = chunkItems.Select(c => c.Content).ToList();
            var vectors = await _embeddingService.GetEmbeddingsBatchAsync(chunkTexts, cancellationToken);

            var documentChunks = new List<DocumentChunk>();
            int totalTokens = 0;

            for (int i = 0; i < chunkItems.Count; i++)
            {
                var item = chunkItems[i];
                var vec = i < vectors.Count ? new Vector(vectors[i].ToArray()) : null;

                totalTokens += item.TokenCount;

                documentChunks.Add(new DocumentChunk
                {
                    DocumentId = doc.Id,
                    ChunkIndex = item.ChunkIndex,
                    Content = item.Content,
                    Embedding = vec,
                    TokenCount = item.TokenCount,
                    MetadataJson = item.MetadataJson
                });
            }

            // Bước 6: Lưu các Chunks vào DB
            await _chunkRepository.AddRangeAsync(documentChunks, cancellationToken);

            // Bước 7: Cập nhật trạng thái INDEXED
            doc.Status = DocumentStatus.INDEXED;
            doc.TotalChunks = documentChunks.Count;
            doc.TotalTokens = totalTokens;
            doc.CrawledAt = DateTime.UtcNow;
            doc.ErrorMessage = null;

            _documentRepository.Update(doc);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Hoàn tất RAG Ingestion thành công cho tài liệu ID {DocumentId}: {Chunks} chunks, {Tokens} tokens.", doc.Id, doc.TotalChunks, doc.TotalTokens);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi trong quy trình RAG Ingestion cho tài liệu ID {DocumentId}: {Message}", documentId, ex.Message);

            try
            {
                _unitOfWork.ClearChangeTracker();

                var failedDoc = await _documentRepository.GetByIdAsync(documentId, CancellationToken.None);
                if (failedDoc != null)
                {
                    failedDoc.Status = DocumentStatus.FAILED;
                    failedDoc.ErrorMessage = ex.Message;
                    _documentRepository.Update(failedDoc);
                    await _unitOfWork.SaveChangesAsync(CancellationToken.None);
                }
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Không thể cập nhật trạng thái FAILED cho tài liệu ID {DocumentId}", documentId);
            }
        }
    }

    public async Task IngestDocumentFromStreamAsync(Guid documentId, Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        var doc = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
        if (doc == null)
        {
            _logger.LogWarning("Không tìm thấy tài liệu ID {DocumentId} để thực hiện Ingestion.", documentId);
            return;
        }

        try
        {
            _logger.LogInformation("Bắt đầu quy trình RAG Ingestion cho tệp tải lên: {Name} ({FileName})", doc.DocumentName, fileName);

            doc.Status = DocumentStatus.PROCESSING;
            doc.ErrorMessage = null;
            _documentRepository.Update(doc);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var cleanText = await _crawlerService.ExtractTextFromStreamAsync(stream, fileName, cancellationToken);
            if (string.IsNullOrWhiteSpace(cleanText))
            {
                throw new InvalidOperationException("Nội dung trích xuất từ tệp tin rỗng.");
            }

            var chunkItems = _chunkerService.ChunkText(cleanText, chunkSize: 600, overlapSize: 100, documentName: doc.DocumentName);
            if (chunkItems.Count == 0)
            {
                throw new InvalidOperationException("Không thể tạo Chunks từ văn bản.");
            }

            _logger.LogInformation("Tài liệu '{Name}' được chia thành {Count} đoạn chunks.", doc.DocumentName, chunkItems.Count);

            await _chunkRepository.DeleteByDocumentIdAsync(doc.Id, cancellationToken);

            var chunkTexts = chunkItems.Select(c => c.Content).ToList();
            var vectors = await _embeddingService.GetEmbeddingsBatchAsync(chunkTexts, cancellationToken);

            var documentChunks = new List<DocumentChunk>();
            int totalTokens = 0;

            for (int i = 0; i < chunkItems.Count; i++)
            {
                var item = chunkItems[i];
                var vec = i < vectors.Count ? new Vector(vectors[i].ToArray()) : null;

                totalTokens += item.TokenCount;

                documentChunks.Add(new DocumentChunk
                {
                    DocumentId = doc.Id,
                    ChunkIndex = item.ChunkIndex,
                    Content = item.Content,
                    Embedding = vec,
                    TokenCount = item.TokenCount,
                    MetadataJson = item.MetadataJson
                });
            }

            await _chunkRepository.AddRangeAsync(documentChunks, cancellationToken);

            doc.Status = DocumentStatus.INDEXED;
            doc.TotalChunks = documentChunks.Count;
            doc.TotalTokens = totalTokens;
            doc.CrawledAt = DateTime.UtcNow;
            doc.ErrorMessage = null;

            _documentRepository.Update(doc);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Hoàn tất RAG Ingestion thành công cho tệp ID {DocumentId}: {Chunks} chunks, {Tokens} tokens.", doc.Id, doc.TotalChunks, doc.TotalTokens);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi trong quy trình RAG Ingestion cho tệp ID {DocumentId}: {Message}", documentId, ex.Message);

            try
            {
                _unitOfWork.ClearChangeTracker();

                var failedDoc = await _documentRepository.GetByIdAsync(documentId, CancellationToken.None);
                if (failedDoc != null)
                {
                    failedDoc.Status = DocumentStatus.FAILED;
                    failedDoc.ErrorMessage = ex.Message;
                    _documentRepository.Update(failedDoc);
                    await _unitOfWork.SaveChangesAsync(CancellationToken.None);
                }
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Không thể cập nhật trạng thái FAILED cho tệp ID {DocumentId}", documentId);
            }
        }
    }
}
