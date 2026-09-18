using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Ai.DTOs.Requests;
using TrustSimBackend.Application.Features.Ai.DTOs.Responses;
using TrustSimBackend.Domain.Entities.System;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Ai.Commands;

public class UploadDocumentCommandHandler : IRequestHandler<UploadDocumentCommand, IngestDocumentResponseDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UploadDocumentCommandHandler> _logger;

    public UploadDocumentCommandHandler(
        IUnitOfWork unitOfWork,
        IServiceProvider serviceProvider,
        ILogger<UploadDocumentCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<IngestDocumentResponseDto> Handle(UploadDocumentCommand request, CancellationToken cancellationToken)
    {
        if (request.File == null || request.File.Length == 0)
        {
            throw new BadRequestException("Tệp tin tải lên không hợp lệ hoặc rỗng.");
        }

        var fileName = request.File.FileName;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var allowedExts = new[] { ".pdf", ".txt", ".md", ".docx", ".html", ".htm", ".csv" };

        if (!allowedExts.Contains(ext))
        {
            throw new BadRequestException($"Định dạng tệp '{ext}' không được hỗ trợ. Vui lòng tải lên file: {string.Join(", ", allowedExts)}");
        }

        // Copy file content to a MemoryStream to process in background
        var memoryStream = new MemoryStream();
        await request.File.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        var doc = new Document
        {
            DocumentName = string.IsNullOrWhiteSpace(request.DocumentName) ? Path.GetFileNameWithoutExtension(fileName) : request.DocumentName.Trim(),
            LinkUrl = $"uploaded://{Guid.NewGuid():N}_{fileName}",
            DocumentType = request.DocumentType,
            Status = DocumentStatus.PENDING
        };

        await _unitOfWork.Documents.AddAsync(doc, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var docId = doc.Id;
        _ = Task.Run(async () =>
        {
            using var scope = _serviceProvider.CreateScope();
            var ingestionService = scope.ServiceProvider.GetRequiredService<IDocumentIngestionService>();
            try
            {
                await ingestionService.IngestDocumentFromStreamAsync(docId, memoryStream, fileName, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background Ingestion thất bại cho tệp tải lên ID {DocumentId}", docId);
            }
            finally
            {
                await memoryStream.DisposeAsync();
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
