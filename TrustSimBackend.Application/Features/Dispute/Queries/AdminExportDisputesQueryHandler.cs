using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Dispute.DTOs.Requests;
using TrustSimBackend.Application.Features.Dispute.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Dispute.Queries;

public class AdminExportDisputesQueryHandler : IRequestHandler<AdminExportDisputesQuery, byte[]>
{
    private readonly IDisputeTicketRepository _disputeRepository;
    private readonly IExcelService _excelService;

    public AdminExportDisputesQueryHandler(IDisputeTicketRepository disputeRepository, IExcelService excelService)
    {
        _disputeRepository = disputeRepository;
        _excelService = excelService;
    }

    public async Task<byte[]> Handle(AdminExportDisputesQuery request, CancellationToken cancellationToken)
    {
        var (items, _) = await _disputeRepository.GetPagedAsync(
            null,
            request.Status,
            pageIndex: 1,
            pageSize: 100000,
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
