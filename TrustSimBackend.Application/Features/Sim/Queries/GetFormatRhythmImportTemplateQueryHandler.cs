using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class GetFormatRhythmImportTemplateQueryHandler : IRequestHandler<GetFormatRhythmImportTemplateQuery, byte[]>
{
    private readonly IExcelService _excelService;

    public GetFormatRhythmImportTemplateQueryHandler(IExcelService excelService)
    {
        _excelService = excelService;
    }

    public async Task<byte[]> Handle(GetFormatRhythmImportTemplateQuery request, CancellationToken cancellationToken)
    {
        var sampleRows = new List<FormatRhythmExcelRow>
        {
            new() { FormatRhythmName = "3-3-4", Value = "0xx.xxx.xxxx", Description = "Định dạng 3 số đầu, 3 số giữa, 4 số đuôi" },
            new() { FormatRhythmName = "4-3-3", Value = "0xxx.xxx.xxx", Description = "Định dạng 4 số đầu, 3 số giữa, 3 số đuôi" }
        };
        return await _excelService.WriteExcelAsync(sampleRows, sheetName: "FormatRhythmsTemplate", cancellationToken: cancellationToken);
    }
}
