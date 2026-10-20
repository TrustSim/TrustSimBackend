using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class ExportSimsQueryHandler : IRequestHandler<ExportSimsQuery, byte[]>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExcelService _excelService;

    public ExportSimsQueryHandler(IUnitOfWork unitOfWork, IExcelService excelService)
    {
        _unitOfWork = unitOfWork;
        _excelService = excelService;
    }

    public async Task<byte[]> Handle(ExportSimsQuery request, CancellationToken cancellationToken)
    {
        var (items, _) = await _unitOfWork.SimNumbers.GetPagedAsync(
            new SimPublicQueryFilter
            {
                Keyword = request.Keyword,
                CarrierId = request.CarrierId,
                CategoryId = request.CategoryId,
                PrefixId = request.PrefixId,
                Status = request.Status,
                PageIndex = 1,
                PageSize = 100000
            },
            cancellationToken);

        var exportRows = items.Select(s => new SimExcelExportRow
        {
            PhoneNumber = s.PhoneNumber,
            CarrierName = s.PhonePrefix?.SimCarrier?.CarrierName ?? string.Empty,
            Prefix = s.PhonePrefix?.PrefixValue ?? string.Empty,
            Categories = string.Join(", ", s.SimCategoryMappings.Select(m => m.SimCategory?.CategoryName).Where(n => !string.IsNullOrEmpty(n))),
            FormatRhythm = s.FormatRhythm?.FormatRhythmName ?? string.Empty,
            Status = s.SimStatus.ToString(),
            CreatedDate = s.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss")
        }).ToList();

        return await _excelService.WriteExcelAsync(exportRows, sheetName: "SimNumbers", cancellationToken: cancellationToken);
    }
}
