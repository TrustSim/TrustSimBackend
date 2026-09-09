using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class ExportSimsQueryHandler : IRequestHandler<ExportSimsQuery, byte[]>
{
    private readonly ISimNumberRepository _simRepository;
    private readonly IExcelService _excelService;

    public ExportSimsQueryHandler(ISimNumberRepository simRepository, IExcelService excelService)
    {
        _simRepository = simRepository;
        _excelService = excelService;
    }

    public async Task<byte[]> Handle(ExportSimsQuery request, CancellationToken cancellationToken)
    {
        var (items, _) = await _simRepository.GetPagedAsync(
            request.Keyword,
            request.CarrierId,
            request.CategoryId,
            request.PrefixId,
            request.Status,
            pageIndex: 1,
            pageSize: 100000,
            cancellationToken: cancellationToken);

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
