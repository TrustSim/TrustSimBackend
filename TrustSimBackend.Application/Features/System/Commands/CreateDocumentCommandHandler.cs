using MediatR;
using TrustSimBackend.Application.Features.System.DTOs.Requests;
using TrustSimBackend.Application.Features.System.DTOs.Responses;
using TrustSimBackend.Domain.Entities.System;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.System.Commands;

public class CreateDocumentCommandHandler : IRequestHandler<CreateDocumentCommand, DocumentDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateDocumentCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<DocumentDto> Handle(CreateDocumentCommand request, CancellationToken cancellationToken)
    {
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            DocumentName = request.DocumentName.Trim(),
            LinkUrl = request.LinkUrl.Trim()
        };

        await _unitOfWork.Documents.AddAsync(doc, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new DocumentDto
        {
            Id = doc.Id,
            DocumentName = doc.DocumentName,
            LinkUrl = doc.LinkUrl,
            DocumentType = doc.DocumentType,
            Status = doc.Status,
            TotalChunks = doc.TotalChunks,
            TotalTokens = doc.TotalTokens,
            CrawledAt = doc.CrawledAt,
            ErrorMessage = doc.ErrorMessage,
            IsActive = doc.IsActive,
            CreatedDate = doc.CreatedDate,
            CreatedBy = doc.CreatedBy,
            UpdatedDate = doc.UpdatedDate,
            UpdatedBy = doc.UpdatedBy,
            IsDeleted = doc.IsDeleted,
            DeletedDate = doc.DeletedDate,
            DeletedBy = doc.DeletedBy
        };
    }
}
