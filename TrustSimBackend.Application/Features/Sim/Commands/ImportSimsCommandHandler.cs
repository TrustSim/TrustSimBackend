using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class ImportSimsCommandHandler : IRequestHandler<ImportSimsCommand, ImportResult<SimNumberDto>>
{
    private readonly IBulkDataService _bulkDataService;
    private readonly IExcelService _excelService;
    private readonly ICsvService _csvService;
    private readonly ISimNumberRepository _simRepository;
    private readonly IPhonePrefixRepository _prefixRepository;
    private readonly ISimCategoryRepository _categoryRepository;
    private readonly IFormatRhythmRepository _rhythmRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ImportSimsCommandHandler(
        IBulkDataService bulkDataService,
        IExcelService excelService,
        ICsvService csvService,
        ISimNumberRepository simRepository,
        IPhonePrefixRepository prefixRepository,
        ISimCategoryRepository categoryRepository,
        IFormatRhythmRepository rhythmRepository,
        IUnitOfWork unitOfWork)
    {
        _bulkDataService = bulkDataService;
        _excelService = excelService;
        _csvService = csvService;
        _simRepository = simRepository;
        _prefixRepository = prefixRepository;
        _categoryRepository = categoryRepository;
        _rhythmRepository = rhythmRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ImportResult<SimNumberDto>> Handle(ImportSimsCommand request, CancellationToken cancellationToken)
    {
        if (request.File == null || request.File.Length == 0)
        {
            throw new BadRequestException("File tải lên trống hoặc không hợp lệ.");
        }

        var ext = Path.GetExtension(request.File.FileName).ToLower();
        if (ext != ".xlsx" && ext != ".csv")
        {
            throw new BadRequestException("Hệ thống chỉ hỗ trợ định dạng file Excel (.xlsx) hoặc CSV (.csv).");
        }

        var result = new ImportResult<SimNumberDto>();
        List<SimExcelImportRow> rows;

        using (var stream = request.File.OpenReadStream())
        {
            if (ext == ".xlsx")
            {
                rows = await _excelService.ReadExcelAsync<SimExcelImportRow>(stream, cancellationToken: cancellationToken);
            }
            else
            {
                rows = await _csvService.ReadCsvAsync<SimExcelImportRow>(stream, cancellationToken: cancellationToken);
            }
        }

        result.TotalRows = rows.Count;
        if (rows.Count == 0)
        {
            return result;
        }

        // Đọc Master Data vào In-Memory Dictionary để mapping cực nhanh
        var allPrefixes = await _prefixRepository.GetAllAsync(null, cancellationToken);
        var allCategories = await _categoryRepository.GetAllAsync(cancellationToken);
        var allRhythms = await _rhythmRepository.GetAllAsync(cancellationToken);
        var defaultRhythm = allRhythms.FirstOrDefault(r => r.FormatRhythmName == "Mặc định") ?? allRhythms.FirstOrDefault();
        var defaultRhythmId = defaultRhythm?.Id ?? await _simRepository.GetOrCreateDefaultFormatRhythmIdAsync(cancellationToken);

        var existingSimsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var simEntitiesToInsert = new List<SimNumber>();
        var ownershipsToInsert = new List<SimOwnership>();
        var categoryMappingsToInsert = new List<SimCategoryMapping>();
        var successDtos = new List<SimNumberDto>();

        var rowIndex = 1;
        foreach (var row in rows)
        {
            rowIndex++;
            if (string.IsNullOrWhiteSpace(row.PhoneNumber))
            {
                result.Errors.Add(new ImportRowError
                {
                    RowIndex = rowIndex,
                    FieldName = nameof(row.PhoneNumber),
                    ErrorMessage = "Số điện thoại không được để trống.",
                    RawData = row.PhoneNumber
                });
                result.FailedCount++;
                continue;
            }

            var cleanPhone = new string(row.PhoneNumber.Where(char.IsDigit).ToArray());
            if (cleanPhone.Length < 9 || cleanPhone.Length > 11)
            {
                result.Errors.Add(new ImportRowError
                {
                    RowIndex = rowIndex,
                    FieldName = nameof(row.PhoneNumber),
                    ErrorMessage = $"Số điện thoại '{row.PhoneNumber}' không đúng độ dài chuẩn (10 số).",
                    RawData = row.PhoneNumber
                });
                result.FailedCount++;
                continue;
            }

            if (!cleanPhone.StartsWith("0"))
            {
                cleanPhone = "0" + cleanPhone;
            }

            if (existingSimsSet.Contains(cleanPhone) || await _simRepository.ExistsByPhoneNumberAsync(cleanPhone, cancellationToken: cancellationToken))
            {
                result.Errors.Add(new ImportRowError
                {
                    RowIndex = rowIndex,
                    FieldName = nameof(row.PhoneNumber),
                    ErrorMessage = $"Số SIM '{cleanPhone}' đã tồn tại trong hệ thống.",
                    RawData = cleanPhone
                });
                result.FailedCount++;
                continue;
            }

            // Tìm PhonePrefix khớp với 3 số đầu
            var matchedPrefix = allPrefixes.FirstOrDefault(p => cleanPhone.StartsWith(p.PrefixValue));
            if (matchedPrefix == null)
            {
                result.Errors.Add(new ImportRowError
                {
                    RowIndex = rowIndex,
                    FieldName = nameof(row.PhoneNumber),
                    ErrorMessage = $"Đầu số của SIM '{cleanPhone}' không thuộc nhà mạng nào được hỗ trợ.",
                    RawData = cleanPhone
                });
                result.FailedCount++;
                continue;
            }

            // Tìm Format Rhythm
            Guid rhythmId = defaultRhythmId;
            if (!string.IsNullOrWhiteSpace(row.FormatRhythmName))
            {
                var matchedRhythm = allRhythms.FirstOrDefault(r => r.FormatRhythmName.Equals(row.FormatRhythmName.Trim(), StringComparison.OrdinalIgnoreCase));
                if (matchedRhythm != null)
                {
                    rhythmId = matchedRhythm.Id;
                }
            }

            var simId = Guid.NewGuid();
            var simEntity = new SimNumber
            {
                Id = simId,
                PhoneNumber = cleanPhone,
                SimStatus = SimStatus.AVAILABLE,
                PhonePrefixId = matchedPrefix.Id,
                FormatRhythmId = rhythmId,
                CreatedDate = DateTime.UtcNow
            };

            simEntitiesToInsert.Add(simEntity);
            existingSimsSet.Add(cleanPhone);

            // Ghi nhận quyền sở hữu của người dùng đăng tải (Seller)
            if (request.UserId != Guid.Empty)
            {
                ownershipsToInsert.Add(new SimOwnership
                {
                    Id = Guid.NewGuid(),
                    SimNumberId = simId,
                    UserAccountId = request.UserId,
                    OwnershipStatus = OwnershipStatus.ACTIVE,
                    AcquistionType = AcquistionType.INITIAL_VERIFICATION,
                    CreatedDate = DateTime.UtcNow
                });
            }

            // Mapping danh mục nếu có
            if (!string.IsNullOrWhiteSpace(row.CategoryNames))
            {
                var catNames = row.CategoryNames.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var catName in catNames)
                {
                    var matchedCat = allCategories.FirstOrDefault(c => c.CategoryName.Equals(catName, StringComparison.OrdinalIgnoreCase));
                    if (matchedCat != null)
                    {
                        categoryMappingsToInsert.Add(new SimCategoryMapping
                        {
                            Id = Guid.NewGuid(),
                            SimNumberId = simId,
                            SimCategoryId = matchedCat.Id,
                            CreatedDate = DateTime.UtcNow
                        });
                    }
                }
            }

            successDtos.Add(new SimNumberDto
            {
                Id = simEntity.Id,
                PhoneNumber = simEntity.PhoneNumber,
                SimStatus = simEntity.SimStatus,
                PhonePrefixId = simEntity.PhonePrefixId,
                PrefixValue = matchedPrefix.PrefixValue,
                CarrierName = matchedPrefix.SimCarrier?.CarrierName ?? string.Empty,
                CarrierLogo = matchedPrefix.SimCarrier?.LogoImg ?? string.Empty,
                FormatRhythmId = simEntity.FormatRhythmId,
                CreatedDate = simEntity.CreatedDate
            });
        }

        // Thực thi Bulk Insert siêu tốc bằng EFCore.BulkExtensions
        if (simEntitiesToInsert.Any())
        {
            await _bulkDataService.BulkInsertAsync(simEntitiesToInsert, cancellationToken: cancellationToken);

            if (ownershipsToInsert.Any())
            {
                await _bulkDataService.BulkInsertAsync(ownershipsToInsert, cancellationToken: cancellationToken);
            }

            if (categoryMappingsToInsert.Any())
            {
                await _bulkDataService.BulkInsertAsync(categoryMappingsToInsert, cancellationToken: cancellationToken);
            }
        }

        result.SuccessCount = successDtos.Count;
        result.SuccessItems = successDtos;

        return result;
    }
}
