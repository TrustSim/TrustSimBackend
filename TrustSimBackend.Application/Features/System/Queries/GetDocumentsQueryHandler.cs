using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.System.DTOs.Requests;
using TrustSimBackend.Application.Features.System.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.System.Queries;

public class GetDocumentsQueryHandler : IRequestHandler<GetDocumentsQuery, PagedResult<DocumentDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetDocumentsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<DocumentDto>> Handle(GetDocumentsQuery request, CancellationToken cancellationToken)
    {
        var (docs, totalCount) = await _unitOfWork.Documents.GetPagedAsync(request.Filter, cancellationToken);
        var dtos = docs.Select(d => new DocumentDto
        {
            Id = d.Id,
            DocumentName = d.DocumentName,
            LinkUrl = d.LinkUrl,
            DocumentType = d.DocumentType,
            Status = d.Status,
            TotalChunks = d.TotalChunks,
            TotalTokens = d.TotalTokens,
            CrawledAt = d.CrawledAt,
            ErrorMessage = d.ErrorMessage,
            IsActive = d.IsActive,
            CreatedDate = d.CreatedDate,
            CreatedBy = d.CreatedBy,
            UpdatedDate = d.UpdatedDate,
            UpdatedBy = d.UpdatedBy,
            IsDeleted = d.IsDeleted,
            DeletedDate = d.DeletedDate,
            DeletedBy = d.DeletedBy
        }).ToList();

        return new PagedResult<DocumentDto>(dtos, totalCount, request.Filter.PageIndex, request.Filter.PageSize);
    }
}
