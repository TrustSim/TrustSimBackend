using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class GetSimImportTemplateQueryHandler : IRequestHandler<GetSimImportTemplateQuery, byte[]>
{
    private readonly IExcelService _excelService;

    public GetSimImportTemplateQueryHandler(IExcelService excelService)
    {
        _excelService = excelService;
    }

    public async Task<byte[]> Handle(GetSimImportTemplateQuery request, CancellationToken cancellationToken)
    {
        var sampleRows = new List<SimExcelImportRow>
        {
            new()
            {
                PhoneNumber = "0988888888",
                CarrierCode = "VTT",
                CategoryNames = "Lục quý, Phong thủy",
                FormatRhythmName = "Lục quý",
                Description = "SIM Lục quý 8 siêu đẹp Viettel"
            },
            new()
            {
                PhoneNumber = "0912345678",
                CarrierCode = "VNP",
                CategoryNames = "Sảnh tiến, Doanh nhân",
                FormatRhythmName = "Sảnh tiến",
                Description = "SIM sảnh tiến 6 số VinaPhone"
            },
            new()
            {
                PhoneNumber = "0909999001",
                CarrierCode = "MBF",
                CategoryNames = "Tam hoa, Thần tài",
                FormatRhythmName = "Tam hoa",
                Description = "SIM MobiFone đẹp"
            }
        };

        return await _excelService.WriteExcelAsync(sampleRows, sheetName: "SimImportTemplate", cancellationToken: cancellationToken);
    }
}
