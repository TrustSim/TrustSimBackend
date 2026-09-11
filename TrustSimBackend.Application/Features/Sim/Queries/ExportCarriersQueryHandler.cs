using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class ExportCarriersQueryHandler : IRequestHandler<ExportCarriersQuery, byte[]>
{
    private readonly ISimCarrierRepository _carrierRepository;
    private readonly IExcelService _excelService;

    public ExportCarriersQueryHandler(ISimCarrierRepository carrierRepository, IExcelService excelService)
    {
        _carrierRepository = carrierRepository;
        _excelService = excelService;
    }

    public async Task<byte[]> Handle(ExportCarriersQuery request, CancellationToken cancellationToken)
    {
        var carriers = await _carrierRepository.GetAllAsync(cancellationToken);
        var rows = carriers.Select(c => new CarrierExcelRow
        {
            CarrierName = c.CarrierName,
            CarrierCode = c.CarrierCode,
            Description = string.Empty,
            LogoImg = c.LogoImg
        }).ToList();
        return await _excelService.WriteExcelAsync(rows, sheetName: "Carriers", cancellationToken: cancellationToken);
    }
}
