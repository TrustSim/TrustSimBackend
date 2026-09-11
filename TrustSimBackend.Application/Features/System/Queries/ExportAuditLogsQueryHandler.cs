using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.System.DTOs.Requests;
using TrustSimBackend.Application.Features.System.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.System.Queries;

public class ExportAuditLogsQueryHandler : IRequestHandler<ExportAuditLogsQuery, byte[]>
{
    private readonly IAuditLogRepository _repository;
    private readonly IExcelService _excelService;

    public ExportAuditLogsQueryHandler(IAuditLogRepository repository, IExcelService excelService)
    {
        _repository = repository;
        _excelService = excelService;
    }

    public async Task<byte[]> Handle(ExportAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var (items, _) = await _repository.GetPagedAsync(
            request.EntityName,
            null,
            null,
            request.ActionType,
            request.FromDate,
            request.ToDate,
            pageIndex: 1,
            pageSize: 100000,
            cancellationToken: cancellationToken);

        var rows = items.Select(log => new AuditLogExcelExportRow
        {
            LogName = log.AuditLogName,
            ActionType = log.ActionType.ToString(),
            EntityName = log.EntityName,
            EntityId = log.EntityId.ToString(),
            ActorName = log.UserAccount?.UserProfile?.FullName ?? log.UserAccount?.UserName ?? string.Empty,
            IPAddress = log.IPAddress,
            CreatedDate = log.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss")
        }).ToList();

        return await _excelService.WriteExcelAsync(rows, sheetName: "AuditLogs", cancellationToken: cancellationToken);
    }
}
