using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.System.DTOs.Requests;
using TrustSimBackend.Application.Features.System.DTOs.Responses;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.System.Queries;

public class ExportDocumentsQueryHandler : IRequestHandler<ExportDocumentsQuery, byte[]>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExcelService _excelService;

    public ExportDocumentsQueryHandler(IUnitOfWork unitOfWork, IExcelService excelService)
    {
        _unitOfWork = unitOfWork;
        _excelService = excelService;
    }

    public async Task<byte[]> Handle(ExportDocumentsQuery request, CancellationToken cancellationToken)
    {
        var exportFilter = request.Filter ?? new DocumentQueryFilter();
        exportFilter.PageIndex = 1;
        exportFilter.PageSize = 100000;

        var (docs, _) = await _unitOfWork.Documents.GetPagedAsync(exportFilter, cancellationToken);
        var rows = docs.Select(d => new DocumentExcelExportRow
        {
            DocumentName = d.DocumentName,
            LinkUrl = d.LinkUrl,
            Status = d.Status.ToString(),
            CreatedDate = d.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss")
        }).ToList();

        return await _excelService.WriteExcelAsync(rows, sheetName: "Documents", cancellationToken: cancellationToken);
    }
}
