using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Dispute.DTOs.Requests;
using TrustSimBackend.Application.Features.Dispute.DTOs.Responses;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Dispute.Queries;

public class AdminExportDisputesQueryHandler : IRequestHandler<AdminExportDisputesQuery, byte[]>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExcelService _excelService;

    public AdminExportDisputesQueryHandler(IUnitOfWork unitOfWork, IExcelService excelService)
    {
        _unitOfWork = unitOfWork;
        _excelService = excelService;
    }

    public async Task<byte[]> Handle(AdminExportDisputesQuery request, CancellationToken cancellationToken)
    {
        var filter = request.Filter ?? new DisputeQueryFilter();
        filter.PageIndex = 1;
        filter.PageSize = 100000;

        var (items, _) = await _unitOfWork.DisputeTickets.GetPagedAsync(
            null,
            filter,
            cancellationToken: cancellationToken);

        var rows = items.Select(d => new DisputeExcelExportRow
        {
            TicketId = d.Id.ToString(),
            TradeTransactionId = d.TradeTransactionId.ToString(),
            ComplainantName = d.Complainant?.UserProfile?.FullName ?? d.Complainant?.UserName ?? string.Empty,
            RespondentName = string.Empty,
            Reason = d.Reason,
            Status = d.Status.ToString(),
            ResolutionResult = d.AdminResolutionNotes ?? string.Empty,
            CreatedDate = d.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss")
        }).ToList();

        return await _excelService.WriteExcelAsync(rows, sheetName: "Disputes", cancellationToken: cancellationToken);
    }
}
