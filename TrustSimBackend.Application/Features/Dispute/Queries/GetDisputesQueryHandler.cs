using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Dispute.DTOs.Requests;
using TrustSimBackend.Application.Features.Dispute.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Dispute.Queries;

public class GetDisputesQueryHandler : IRequestHandler<GetDisputesQuery, PagedResult<DisputeTicketDto>>
{
    private readonly IDisputeTicketRepository _disputeRepository;

    public GetDisputesQueryHandler(IDisputeTicketRepository disputeRepository)
    {
        _disputeRepository = disputeRepository;
    }

    public async Task<PagedResult<DisputeTicketDto>> Handle(GetDisputesQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _disputeRepository.GetPagedAsync(
            request.UserId,
            request.Status,
            request.PageIndex,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(d => new DisputeTicketDto
        {
            Id = d.Id,
            TradeTransactionId = d.TradeTransactionId,
            ComplainantId = d.ComplainantId,
            ComplainantName = d.Complainant?.UserProfile?.FullName ?? d.Complainant?.UserName ?? string.Empty,
            Reason = d.Reason,
            Status = d.Status,
            ResolvedByAdminId = d.ResolvedByAdminId,
            AdminResolutionNotes = d.AdminResolutionNotes,
            CreatedDate = d.CreatedDate
        }).ToList();

        return PagedResult<DisputeTicketDto>.Create(dtos, totalCount, request.PageIndex, request.PageSize);
    }
}
