using MediatR;
using TrustSimBackend.Application.Features.System.DTOs.Requests;
using TrustSimBackend.Application.Features.System.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.System.Queries;

public class GetDocumentsQueryHandler : IRequestHandler<GetDocumentsQuery, IReadOnlyList<DocumentDto>>
{
    private readonly IDocumentRepository _documentRepository;

    public GetDocumentsQueryHandler(IDocumentRepository documentRepository)
    {
        _documentRepository = documentRepository;
    }

    public async Task<IReadOnlyList<DocumentDto>> Handle(GetDocumentsQuery request, CancellationToken cancellationToken)
    {
        var docs = await _documentRepository.GetAllAsync(request.ActiveOnly, cancellationToken);
        return docs.Select(d => new DocumentDto
        {
            Id = d.Id,
            DocumentName = d.DocumentName,
            LinkUrl = d.LinkUrl,
            CreatedDate = d.CreatedDate
        }).ToList();
    }
}
