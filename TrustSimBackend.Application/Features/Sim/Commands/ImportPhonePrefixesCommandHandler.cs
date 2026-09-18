using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class ImportPhonePrefixesCommandHandler : IRequestHandler<ImportPhonePrefixesCommand, ImportResult<PhonePrefixDto>>
{
    private readonly IBulkDataService _bulkDataService;
    private readonly IExcelService _excelService;
    private readonly IUnitOfWork _unitOfWork;

    public ImportPhonePrefixesCommandHandler(
        IBulkDataService bulkDataService,
        IExcelService excelService,
        IUnitOfWork unitOfWork)
    {
        _bulkDataService = bulkDataService;
        _excelService = excelService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ImportResult<PhonePrefixDto>> Handle(ImportPhonePrefixesCommand request, CancellationToken cancellationToken)
    {
        if (request.File == null || request.File.Length == 0) throw new BadRequestException("File tải lên trống.");
        using var stream = request.File.OpenReadStream();
        var rows = await _excelService.ReadExcelAsync<PrefixExcelRow>(stream, cancellationToken: cancellationToken);

        var result = new ImportResult<PhonePrefixDto> { TotalRows = rows.Count };
        var carriers = await _unitOfWork.SimCarriers.GetAllAsync(cancellationToken);
        var carrierMap = carriers.ToDictionary(c => c.CarrierCode.ToUpper(), c => c.Id);

        var entities = new List<PhonePrefix>();
        var dtos = new List<PhonePrefixDto>();
        int idx = 1;

        foreach (var r in rows)
        {
            idx++;
            if (string.IsNullOrWhiteSpace(r.PrefixValue) || string.IsNullOrWhiteSpace(r.CarrierCode))
            {
                result.Errors.Add(new ImportRowError { RowIndex = idx, FieldName = "PrefixValue", ErrorMessage = "Đầu số và mã nhà mạng không được trống." });
                result.FailedCount++;
                continue;
            }

            var prefix = r.PrefixValue.Trim();
            var carrierCode = r.CarrierCode.Trim().ToUpper();

            if (!carrierMap.TryGetValue(carrierCode, out var carrierId))
            {
                result.Errors.Add(new ImportRowError { RowIndex = idx, FieldName = "CarrierCode", ErrorMessage = $"Không tìm thấy nhà mạng với mã '{carrierCode}'." });
                result.FailedCount++;
                continue;
            }

            if (await _unitOfWork.PhonePrefixes.ExistsByValueAsync(prefix, cancellationToken: cancellationToken))
            {
                result.Errors.Add(new ImportRowError { RowIndex = idx, FieldName = "PrefixValue", ErrorMessage = $"Đầu số '{prefix}' đã tồn tại." });
                result.FailedCount++;
                continue;
            }

            var p = new PhonePrefix
            {
                Id = Guid.NewGuid(),
                PrefixValue = prefix,
                SimCarrierId = carrierId,
                CreatedDate = DateTime.UtcNow
            };
            entities.Add(p);
            dtos.Add(new PhonePrefixDto { Id = p.Id, PrefixValue = p.PrefixValue, SimCarrierId = p.SimCarrierId, CarrierName = carriers.First(c => c.Id == carrierId).CarrierName });
        }

        if (entities.Any()) await _bulkDataService.BulkInsertAsync(entities, cancellationToken: cancellationToken);
        result.SuccessCount = dtos.Count;
        result.SuccessItems = dtos;
        return result;
    }
}
