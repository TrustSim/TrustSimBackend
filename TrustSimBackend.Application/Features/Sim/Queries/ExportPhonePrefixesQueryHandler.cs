using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Queries;

public class ExportPhonePrefixesQueryHandler : IRequestHandler<ExportPhonePrefixesQuery, byte[]>
{
    private readonly IPhonePrefixRepository _prefixRepository;
    private readonly IExcelService _excelService;

    public ExportPhonePrefixesQueryHandler(IPhonePrefixRepository prefixRepository, IExcelService excelService)
    {
        _prefixRepository = prefixRepository;
        _excelService = excelService;
    }

    public async Task<byte[]> Handle(ExportPhonePrefixesQuery request, CancellationToken cancellationToken)
    {
        var prefixes = await _prefixRepository.GetAllAsync(request.CarrierId, cancellationToken);
        var rows = prefixes.Select(p => new PrefixExcelRow
        {
            PrefixValue = p.PrefixValue,
            CarrierCode = p.SimCarrier?.CarrierCode ?? string.Empty
        }).ToList();
        return await _excelService.WriteExcelAsync(rows, sheetName: "PhonePrefixes", cancellationToken: cancellationToken);
    }
}
