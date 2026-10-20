using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Dispute.DTOs.Requests;
using TrustSimBackend.Application.Features.Dispute.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Dispute.Queries;

public class GetDisputesQueryHandler : IRequestHandler<GetDisputesQuery, PagedResult<DisputeTicketDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetDisputesQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<DisputeTicketDto>> Handle(GetDisputesQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _unitOfWork.DisputeTickets.GetPagedAsync(
            request.UserId,
            request.Filter,
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

        return PagedResult<DisputeTicketDto>.Create(dtos, totalCount, request.Filter.PageIndex, request.Filter.PageSize);
    }
}
