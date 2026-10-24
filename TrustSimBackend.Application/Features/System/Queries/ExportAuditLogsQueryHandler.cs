using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.System.DTOs.Requests;
using TrustSimBackend.Application.Features.System.DTOs.Responses;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.System.Queries;

public class ExportAuditLogsQueryHandler : IRequestHandler<ExportAuditLogsQuery, byte[]>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExcelService _excelService;

    public ExportAuditLogsQueryHandler(IUnitOfWork unitOfWork, IExcelService excelService)
    {
        _unitOfWork = unitOfWork;
        _excelService = excelService;
    }

    public async Task<byte[]> Handle(ExportAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var filter = request.Filter ?? new AuditLogQueryFilter();
        filter.PageIndex = 1;
        filter.PageSize = 100000;

        var (items, _) = await _unitOfWork.AuditLogs.GetPagedAsync(
            filter,
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
