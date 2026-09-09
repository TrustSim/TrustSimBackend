using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class GetCategoryImportTemplateQueryHandler : IRequestHandler<GetCategoryImportTemplateQuery, byte[]>
{
    private readonly IExcelService _excelService;

    public GetCategoryImportTemplateQueryHandler(IExcelService excelService)
    {
        _excelService = excelService;
    }

    public async Task<byte[]> Handle(GetCategoryImportTemplateQuery request, CancellationToken cancellationToken)
    {
        var sampleRows = new List<CategoryExcelRow>
        {
            new() { CategoryCode = "TU_QUY", CategoryName = "Tứ Quý", Description = "SIM có 4 số đuôi giống nhau", ValuationMultiplier = 1.5f },
            new() { CategoryCode = "NGU_QUY", CategoryName = "Ngũ Quý", Description = "SIM có 5 số đuôi giống nhau", ValuationMultiplier = 2.0f }
        };
        return await _excelService.WriteExcelAsync(sampleRows, sheetName: "CategoriesTemplate", cancellationToken: cancellationToken);
    }
}
