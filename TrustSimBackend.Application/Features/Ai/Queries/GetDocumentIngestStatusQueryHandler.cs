using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Ai.DTOs.Requests;
using TrustSimBackend.Application.Features.Ai.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Ai.Queries;

public class GetDocumentIngestStatusQueryHandler : IRequestHandler<GetDocumentIngestStatusQuery, IngestDocumentResponseDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetDocumentIngestStatusQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IngestDocumentResponseDto> Handle(GetDocumentIngestStatusQuery request, CancellationToken cancellationToken)
    {
        var doc = await _unitOfWork.Documents.GetByIdAsync(request.DocumentId, cancellationToken);
        if (doc == null)
        {
            throw new NotFoundException($"Không tìm thấy tài liệu ID {request.DocumentId}");
        }

        return new IngestDocumentResponseDto
        {
            DocumentId = doc.Id,
            DocumentName = doc.DocumentName,
            LinkUrl = doc.LinkUrl,
            DocumentType = doc.DocumentType,
            Status = doc.Status,
            TotalChunks = doc.TotalChunks,
            TotalTokens = doc.TotalTokens,
            CrawledAt = doc.CrawledAt,
            ErrorMessage = doc.ErrorMessage
        };
    }
}
