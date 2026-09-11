using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class GetCarrierImportTemplateQueryHandler : IRequestHandler<GetCarrierImportTemplateQuery, byte[]>
{
    private readonly IExcelService _excelService;

    public GetCarrierImportTemplateQueryHandler(IExcelService excelService)
    {
        _excelService = excelService;
    }

    public async Task<byte[]> Handle(GetCarrierImportTemplateQuery request, CancellationToken cancellationToken)
    {
        var sampleRows = new List<CarrierExcelRow>
        {
            new() { CarrierName = "Viettel", CarrierCode = "VIETTEL", Description = "Tập đoàn Công nghiệp - Viễn thông Quân đội", LogoImg = "https://assets.trustsim.vn/carriers/viettel.png" },
            new() { CarrierName = "Vinaphone", CarrierCode = "VINAPHONE", Description = "Tập đoàn Bưu chính Viễn thông VNPT", LogoImg = "https://assets.trustsim.vn/carriers/vinaphone.png" }
        };
        return await _excelService.WriteExcelAsync(sampleRows, sheetName: "CarriersTemplate", cancellationToken: cancellationToken);
    }
}
