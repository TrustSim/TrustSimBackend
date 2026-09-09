using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class ExportFormatRhythmsQueryHandler : IRequestHandler<ExportFormatRhythmsQuery, byte[]>
{
    private readonly IFormatRhythmRepository _repository;
    private readonly IExcelService _excelService;

    public ExportFormatRhythmsQueryHandler(IFormatRhythmRepository repository, IExcelService excelService)
    {
        _repository = repository;
        _excelService = excelService;
    }

    public async Task<byte[]> Handle(ExportFormatRhythmsQuery request, CancellationToken cancellationToken)
    {
        var list = await _repository.GetAllAsync(cancellationToken);
        var rows = list.Select(r => new FormatRhythmExcelRow
        {
            FormatRhythmName = r.FormatRhythmName,
            Value = r.Value,
            Description = r.Description
        }).ToList();
        return await _excelService.WriteExcelAsync(rows, sheetName: "FormatRhythms", cancellationToken: cancellationToken);
    }
}
