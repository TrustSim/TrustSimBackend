using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.System.DTOs.Requests;
using TrustSimBackend.Application.Features.System.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.System.Queries;

public class GetDocumentByIdQueryHandler : IRequestHandler<GetDocumentByIdQuery, DocumentDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetDocumentByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<DocumentDto> Handle(GetDocumentByIdQuery request, CancellationToken cancellationToken)
    {
        var doc = await _unitOfWork.Documents.GetByIdAsync(request.Id, cancellationToken);
        if (doc == null)
        {
            throw new NotFoundException($"Không tìm thấy văn bản với ID '{request.Id}'.");
        }

        return new DocumentDto
        {
            Id = doc.Id,
            DocumentName = doc.DocumentName,
            LinkUrl = doc.LinkUrl,
            CreatedDate = doc.CreatedDate
        };
    }
}
