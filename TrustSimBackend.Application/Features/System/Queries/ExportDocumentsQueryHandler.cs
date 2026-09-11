using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.System.DTOs.Requests;
using TrustSimBackend.Application.Features.System.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.System.Queries;

public class ExportDocumentsQueryHandler : IRequestHandler<ExportDocumentsQuery, byte[]>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IExcelService _excelService;

    public ExportDocumentsQueryHandler(IDocumentRepository documentRepository, IExcelService excelService)
    {
        _documentRepository = documentRepository;
        _excelService = excelService;
    }

    public async Task<byte[]> Handle(ExportDocumentsQuery request, CancellationToken cancellationToken)
    {
        var docs = await _documentRepository.GetAllAsync(request.ActiveOnly ?? false, cancellationToken);
        var rows = docs.Select(d => new DocumentExcelExportRow
        {
            DocumentName = d.DocumentName,
            LinkUrl = d.LinkUrl,
            Status = d.IsDeleted ? "Đã xóa" : "Hoạt động",
            CreatedDate = d.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss")
        }).ToList();

        return await _excelService.WriteExcelAsync(rows, sheetName: "Documents", cancellationToken: cancellationToken);
    }
}
