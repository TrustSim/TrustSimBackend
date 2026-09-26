using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.System.DTOs.Requests;
using TrustSimBackend.Application.Features.System.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.System.Commands;

public class UpdateDocumentCommandHandler : IRequestHandler<UpdateDocumentCommand, DocumentDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDocumentCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<DocumentDto> Handle(UpdateDocumentCommand request, CancellationToken cancellationToken)
    {
        var doc = await _unitOfWork.Documents.GetByIdAsync(request.Id, cancellationToken);
        if (doc == null)
        {
            throw new NotFoundException($"Không tìm thấy văn bản với ID '{request.Id}'.");
        }

        doc.DocumentName = request.DocumentName.Trim();
        doc.LinkUrl = request.LinkUrl.Trim();

        _unitOfWork.Documents.Update(doc);
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
