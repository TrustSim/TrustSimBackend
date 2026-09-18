using FluentValidation;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Common.Services;

/// <summary>
/// Lớp cơ sở trừu tượng (Template Method Pattern) phục vụ Import/Export dữ liệu hàng loạt
/// tích hợp xác thực dữ liệu qua FluentValidation và thực thi siêu tốc qua IBulkDataService.
/// </summary>
/// <typeparam name="TDto">Kiểu dữ liệu DTO ánh xạ từ dòng file CSV/Excel.</typeparam>
/// <typeparam name="TEntity">Kiểu dữ liệu Entity lưu trong Cơ sở dữ liệu.</typeparam>
public abstract class BaseImportExportService<TDto, TEntity>
    where TDto : class, new()
    where TEntity : class
{
    protected readonly IBulkDataService BulkDataService;
    protected readonly ICsvService CsvService;
    protected readonly IExcelService ExcelService;
    protected readonly IValidator<TDto>? Validator;
    protected readonly ILogger Logger;

    protected BaseImportExportService(
        IBulkDataService bulkDataService,
        ICsvService csvService,
        IExcelService excelService,
        IValidator<TDto>? validator,
        ILogger logger)
    {
        BulkDataService = bulkDataService;
        CsvService = csvService;
        ExcelService = excelService;
        Validator = validator;
        Logger = logger;
    }

    /// <summary>
    /// Phương thức chuyển đổi bắt buộc từ DTO sang Domain Entity.
    /// </summary>
    protected abstract TEntity MapToEntity(TDto dto, int rowIndex);

    /// <summary>
    /// Hook tùy chọn để thực hiện tiền xử lý hoặc kiểm tra bổ sung (VD: kiểm tra trùng lặp trong DB).
    /// </summary>
    protected virtual Task<List<ImportRowError>> CustomValidateBatchAsync(List<TDto> dtos, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<ImportRowError>());
    }

    /// <summary>
    /// Thực hiện toàn bộ quy trình import file (Đọc file -> Validate từng dòng -> Chuyển đổi -> Bulk Insert).
    /// </summary>
    public async Task<ImportResult<TEntity>> ImportAsync(
        Stream stream,
        ImportExportFormat format,
        bool isUpsert = false,
        Action<BulkOperationOptions>? configureBulkOptions = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ImportResult<TEntity>();
        var validEntities = new List<TEntity>();
        var rawDtos = new List<TDto>();

        int currentRowIndex = 1; // Dòng 1 thường là header

        try
        {
            // 1. Đọc dữ liệu từ file theo định dạng
            if (format == ImportExportFormat.Csv)
            {
                await foreach (var dto in CsvService.ReadCsvStreamAsync<TDto>(stream, cancellationToken))
                {
                    currentRowIndex++;
                    rawDtos.Add(dto);
                    await ValidateAndProcessDtoAsync(dto, currentRowIndex, validEntities, result, cancellationToken);
                }
            }
            else
            {
                await foreach (var dto in ExcelService.ReadExcelStreamAsync<TDto>(stream, cancellationToken: cancellationToken))
                {
                    currentRowIndex++;
                    rawDtos.Add(dto);
                    await ValidateAndProcessDtoAsync(dto, currentRowIndex, validEntities, result, cancellationToken);
                }
            }

            result.TotalRows = rawDtos.Count;

            // 2. Chạy custom validation cấp batch (nếu có)
            var batchErrors = await CustomValidateBatchAsync(rawDtos, cancellationToken);
            if (batchErrors.Any())
            {
                result.Errors.AddRange(batchErrors);
            }

            // 3. Nếu có dữ liệu hợp lệ, thực hiện Bulk Operation vào Database
            if (validEntities.Any())
            {
                if (isUpsert)
                {
                    await BulkDataService.BulkInsertOrUpdateAsync(validEntities, configureBulkOptions, cancellationToken);
                }
                else
                {
                    await BulkDataService.BulkInsertAsync(validEntities, configureBulkOptions, cancellationToken);
                }

                result.SuccessCount = validEntities.Count;
                result.SuccessItems = validEntities;
            }

            result.FailedCount = result.Errors.Select(e => e.RowIndex).Distinct().Count();

            Logger.LogInformation("Import hoàn tất: Tổng {Total}, Thành công {Success}, Lỗi {Failed}",
                result.TotalRows, result.SuccessCount, result.FailedCount);

            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Lỗi xảy ra trong quá trình Import dữ liệu: {Message}", ex.Message);
            result.Errors.Add(new ImportRowError
            {
                RowIndex = currentRowIndex,
                FieldName = "System",
                ErrorMessage = $"Lỗi xử lý file: {ex.Message}"
            });
            return result;
        }
    }

    private async Task ValidateAndProcessDtoAsync(
        TDto dto,
        int rowIndex,
        List<TEntity> validEntities,
        ImportResult<TEntity> result,
        CancellationToken cancellationToken)
    {
        if (Validator != null)
        {
            var validationResult = await Validator.ValidateAsync(dto, cancellationToken);
            if (!validationResult.IsValid)
            {
                foreach (var err in validationResult.Errors)
                {
                    result.Errors.Add(new ImportRowError
                    {
                        RowIndex = rowIndex,
                        FieldName = err.PropertyName,
                        ErrorMessage = err.ErrorMessage
                    });
                }
                return;
            }
        }

        try
        {
            var entity = MapToEntity(dto, rowIndex);
            validEntities.Add(entity);
        }
        catch (Exception ex)
        {
            result.Errors.Add(new ImportRowError
            {
                RowIndex = rowIndex,
                FieldName = "Mapping",
                ErrorMessage = $"Lỗi ánh xạ dữ liệu: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Xuất danh sách dữ liệu ra định dạng file mong muốn (CSV hoặc Excel).
    /// </summary>
    public async Task<byte[]> ExportAsync(
        IEnumerable<TDto> records,
        ImportExportFormat format,
        string sheetName = "Sheet1",
        CancellationToken cancellationToken = default)
    {
        return format switch
        {
            ImportExportFormat.Csv => await CsvService.WriteCsvAsync(records, cancellationToken),
            ImportExportFormat.Excel => await ExcelService.WriteExcelAsync(records, sheetName, cancellationToken),
            _ => throw new NotSupportedException($"Định dạng {format} chưa được hỗ trợ.")
        };
    }
}
