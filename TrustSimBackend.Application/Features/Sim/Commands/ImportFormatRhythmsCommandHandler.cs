using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class ImportFormatRhythmsCommandHandler : IRequestHandler<ImportFormatRhythmsCommand, ImportResult<FormatRhythmDto>>
{
    private readonly IBulkDataService _bulkDataService;
    private readonly IExcelService _excelService;
    private readonly IFormatRhythmRepository _repository;

    public ImportFormatRhythmsCommandHandler(IBulkDataService bulkDataService, IExcelService excelService, IFormatRhythmRepository repository)
    {
        _bulkDataService = bulkDataService;
        _excelService = excelService;
        _repository = repository;
    }

    public async Task<ImportResult<FormatRhythmDto>> Handle(ImportFormatRhythmsCommand request, CancellationToken cancellationToken)
    {
        if (request.File == null || request.File.Length == 0) throw new BadRequestException("File tải lên trống.");
        using var stream = request.File.OpenReadStream();
        var rows = await _excelService.ReadExcelAsync<FormatRhythmExcelRow>(stream, cancellationToken: cancellationToken);

        var result = new ImportResult<FormatRhythmDto> { TotalRows = rows.Count };
        var entities = new List<FormatRhythm>();
        var dtos = new List<FormatRhythmDto>();
        int idx = 1;

        foreach (var r in rows)
        {
            idx++;
            if (string.IsNullOrWhiteSpace(r.FormatRhythmName) || string.IsNullOrWhiteSpace(r.Value))
            {
                result.Errors.Add(new ImportRowError { RowIndex = idx, FieldName = "FormatRhythmName", ErrorMessage = "Tên nhịp và định dạng giá trị không được để trống." });
                result.FailedCount++;
                continue;
            }

            var name = r.FormatRhythmName.Trim();
            if (await _repository.ExistsByNameAsync(name, cancellationToken: cancellationToken))
            {
                result.Errors.Add(new ImportRowError { RowIndex = idx, FieldName = "FormatRhythmName", ErrorMessage = $"Tên nhịp số '{name}' đã tồn tại." });
                result.FailedCount++;
                continue;
            }

            var rhythm = new FormatRhythm
            {
                Id = Guid.NewGuid(),
                FormatRhythmName = name,
                Value = r.Value.Trim(),
                Description = r.Description?.Trim() ?? string.Empty,
                CreatedDate = DateTime.UtcNow
            };
            entities.Add(rhythm);
            dtos.Add(new FormatRhythmDto { Id = rhythm.Id, FormatRhythmName = rhythm.FormatRhythmName, Value = rhythm.Value, Description = rhythm.Description });
        }

        if (entities.Any()) await _bulkDataService.BulkInsertAsync(entities, cancellationToken: cancellationToken);
        result.SuccessCount = dtos.Count;
        result.SuccessItems = dtos;
        return result;
    }
}
