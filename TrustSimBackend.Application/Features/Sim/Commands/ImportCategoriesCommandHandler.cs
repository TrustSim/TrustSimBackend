using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class ImportCategoriesCommandHandler : IRequestHandler<ImportCategoriesCommand, ImportResult<SimCategoryDto>>
{
    private readonly IBulkDataService _bulkDataService;
    private readonly IExcelService _excelService;
    private readonly ISimCategoryRepository _categoryRepository;

    public ImportCategoriesCommandHandler(IBulkDataService bulkDataService, IExcelService excelService, ISimCategoryRepository categoryRepository)
    {
        _bulkDataService = bulkDataService;
        _excelService = excelService;
        _categoryRepository = categoryRepository;
    }

    public async Task<ImportResult<SimCategoryDto>> Handle(ImportCategoriesCommand request, CancellationToken cancellationToken)
    {
        if (request.File == null || request.File.Length == 0) throw new BadRequestException("File tải lên trống.");
        using var stream = request.File.OpenReadStream();
        var rows = await _excelService.ReadExcelAsync<CategoryExcelRow>(stream, cancellationToken: cancellationToken);

        var result = new ImportResult<SimCategoryDto> { TotalRows = rows.Count };
        var entities = new List<SimCategory>();
        var dtos = new List<SimCategoryDto>();
        int idx = 1;

        foreach (var r in rows)
        {
            idx++;
            if (string.IsNullOrWhiteSpace(r.CategoryName) || string.IsNullOrWhiteSpace(r.CategoryCode))
            {
                result.Errors.Add(new ImportRowError { RowIndex = idx, FieldName = "CategoryCode", ErrorMessage = "Tên thể loại và mã thể loại không được để trống." });
                result.FailedCount++;
                continue;
            }

            var code = r.CategoryCode.Trim().ToUpper();
            if (await _categoryRepository.ExistsByCodeAsync(code, cancellationToken: cancellationToken))
            {
                result.Errors.Add(new ImportRowError { RowIndex = idx, FieldName = "CategoryCode", ErrorMessage = $"Mã thể loại '{code}' đã tồn tại." });
                result.FailedCount++;
                continue;
            }

            var cat = new SimCategory
            {
                Id = Guid.NewGuid(),
                CategoryCode = code,
                CategoryName = r.CategoryName.Trim(),
                Description = r.Description?.Trim() ?? string.Empty,
                ValuationMultiplier = r.ValuationMultiplier > 0 ? r.ValuationMultiplier : 1.0f,
                CreatedDate = DateTime.UtcNow
            };
            entities.Add(cat);
            dtos.Add(new SimCategoryDto { Id = cat.Id, CategoryName = cat.CategoryName, Description = cat.Description, ValuationMultiplier = cat.ValuationMultiplier });
        }

        if (entities.Any()) await _bulkDataService.BulkInsertAsync(entities, cancellationToken: cancellationToken);
        result.SuccessCount = dtos.Count;
        result.SuccessItems = dtos;
        return result;
    }
}
