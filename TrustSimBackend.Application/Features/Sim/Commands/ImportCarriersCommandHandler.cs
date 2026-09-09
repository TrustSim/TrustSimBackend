using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class ImportCarriersCommandHandler : IRequestHandler<ImportCarriersCommand, ImportResult<SimCarrierDto>>
{
    private readonly IBulkDataService _bulkDataService;
    private readonly IExcelService _excelService;
    private readonly ISimCarrierRepository _carrierRepository;

    public ImportCarriersCommandHandler(IBulkDataService bulkDataService, IExcelService excelService, ISimCarrierRepository carrierRepository)
    {
        _bulkDataService = bulkDataService;
        _excelService = excelService;
        _carrierRepository = carrierRepository;
    }

    public async Task<ImportResult<SimCarrierDto>> Handle(ImportCarriersCommand request, CancellationToken cancellationToken)
    {
        if (request.File == null || request.File.Length == 0) throw new BadRequestException("File tải lên trống.");
        using var stream = request.File.OpenReadStream();
        var rows = await _excelService.ReadExcelAsync<CarrierExcelRow>(stream, cancellationToken: cancellationToken);

        var result = new ImportResult<SimCarrierDto> { TotalRows = rows.Count };
        var entities = new List<SimCarrier>();
        var dtos = new List<SimCarrierDto>();
        int idx = 1;

        foreach (var r in rows)
        {
            idx++;
            if (string.IsNullOrWhiteSpace(r.CarrierName) || string.IsNullOrWhiteSpace(r.CarrierCode))
            {
                result.Errors.Add(new ImportRowError { RowIndex = idx, FieldName = "CarrierName", ErrorMessage = "Tên nhà mạng và mã định danh không được trống." });
                result.FailedCount++;
                continue;
            }

            var code = r.CarrierCode.Trim().ToUpper();
            if (await _carrierRepository.ExistsByCodeAsync(code, cancellationToken: cancellationToken))
            {
                result.Errors.Add(new ImportRowError { RowIndex = idx, FieldName = "CarrierCode", ErrorMessage = $"Mã nhà mạng '{code}' đã tồn tại." });
                result.FailedCount++;
                continue;
            }

            var carrier = new SimCarrier
            {
                Id = Guid.NewGuid(),
                CarrierName = r.CarrierName.Trim(),
                CarrierCode = code,
                LogoImg = r.LogoImg ?? "https://assets.trustsim.vn/carriers/default.png",
                CreatedDate = DateTime.UtcNow
            };
            entities.Add(carrier);
            dtos.Add(new SimCarrierDto { Id = carrier.Id, CarrierName = carrier.CarrierName, CarrierCode = carrier.CarrierCode, LogoImg = carrier.LogoImg });
        }

        if (entities.Any()) await _bulkDataService.BulkInsertAsync(entities, cancellationToken: cancellationToken);
        result.SuccessCount = dtos.Count;
        result.SuccessItems = dtos;
        return result;
    }
}
