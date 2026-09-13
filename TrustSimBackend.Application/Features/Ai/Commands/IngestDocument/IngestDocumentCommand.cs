using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Ai.DTOs;
using TrustSimBackend.Domain.Entities.System;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Ai.Commands.IngestDocument;

public record IngestDocumentCommand(
    string DocumentName,
    string LinkUrl,
    DocumentType DocumentType = DocumentType.MARKET_RESEARCH
) : IRequest<IngestDocumentResponseDto>;

public class IngestDocumentCommandHandler : IRequestHandler<IngestDocumentCommand, IngestDocumentResponseDto>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IngestDocumentCommandHandler> _logger;

    public IngestDocumentCommandHandler(
        IDocumentRepository documentRepository,
        IUnitOfWork unitOfWork,
        IServiceProvider serviceProvider,
        ILogger<IngestDocumentCommandHandler> logger)
    {
        _documentRepository = documentRepository;
        _unitOfWork = unitOfWork;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<IngestDocumentResponseDto> Handle(IngestDocumentCommand request, CancellationToken cancellationToken)
    {
        var doc = new Document
        {
            DocumentName = request.DocumentName.Trim(),
            LinkUrl = request.LinkUrl.Trim(),
            DocumentType = request.DocumentType,
            Status = DocumentStatus.PENDING
        };

        await _documentRepository.AddAsync(doc, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Kích hoạt tiến trình Ingestion bất đồng bộ trong nền
        var docId = doc.Id;
        _ = Task.Run(async () =>
        {
            using var scope = _serviceProvider.CreateScope();
            var ingestionService = scope.ServiceProvider.GetRequiredService<IDocumentIngestionService>();
            try
            {
                await ingestionService.IngestDocumentAsync(docId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background Ingestion thất bại cho tài liệu ID {DocumentId}", docId);
            }
        });

        return new IngestDocumentResponseDto
        {
            DocumentId = doc.Id,
            DocumentName = doc.DocumentName,
            LinkUrl = doc.LinkUrl,
            DocumentType = doc.DocumentType,
            Status = doc.Status,
            TotalChunks = 0,
            TotalTokens = 0,
            CrawledAt = null,
            ErrorMessage = null
        };
    }
}
