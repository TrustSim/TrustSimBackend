using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class GetPhonePrefixImportTemplateQueryHandler : IRequestHandler<GetPhonePrefixImportTemplateQuery, byte[]>
{
    private readonly IExcelService _excelService;

    public GetPhonePrefixImportTemplateQueryHandler(IExcelService excelService)
    {
        _excelService = excelService;
    }

    public async Task<byte[]> Handle(GetPhonePrefixImportTemplateQuery request, CancellationToken cancellationToken)
    {
        var sampleRows = new List<PrefixExcelRow>
        {
            new() { PrefixValue = "098", CarrierCode = "VIETTEL" },
            new() { PrefixValue = "091", CarrierCode = "VINAPHONE" }
        };
        return await _excelService.WriteExcelAsync(sampleRows, sheetName: "PhonePrefixesTemplate", cancellationToken: cancellationToken);
    }
}
