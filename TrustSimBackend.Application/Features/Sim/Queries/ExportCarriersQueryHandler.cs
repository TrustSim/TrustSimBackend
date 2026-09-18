using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class ExportCarriersQueryHandler : IRequestHandler<ExportCarriersQuery, byte[]>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExcelService _excelService;

    public ExportCarriersQueryHandler(IUnitOfWork unitOfWork, IExcelService excelService)
    {
        _unitOfWork = unitOfWork;
        _excelService = excelService;
    }

    public async Task<byte[]> Handle(ExportCarriersQuery request, CancellationToken cancellationToken)
    {
        var carriers = await _unitOfWork.SimCarriers.GetAllAsync(cancellationToken);
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
