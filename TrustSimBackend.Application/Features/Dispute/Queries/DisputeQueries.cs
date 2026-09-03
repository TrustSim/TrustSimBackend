using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Dispute.DTOs;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Dispute.Queries;

public record GetDisputesQuery(
    Guid? UserId = null,
    DisputeStatus? Status = null,
    int PageIndex = 1,
    int PageSize = 20
) : IRequest<ApiResponse<PagedResult<DisputeTicketDto>>>;

public class GetDisputesQueryHandler : IRequestHandler<GetDisputesQuery, ApiResponse<PagedResult<DisputeTicketDto>>>
{
    private readonly IDisputeTicketRepository _disputeRepository;

    public GetDisputesQueryHandler(IDisputeTicketRepository disputeRepository)
    {
        _disputeRepository = disputeRepository;
    }

    public async Task<ApiResponse<PagedResult<DisputeTicketDto>>> Handle(GetDisputesQuery request, CancellationToken cancellationToken)
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

        var pagedResult = PagedResult<DisputeTicketDto>.Create(dtos, totalCount, request.PageIndex, request.PageSize);
        return ApiResponse<PagedResult<DisputeTicketDto>>.SuccessResult(pagedResult, "Lấy danh sách khiếu nại thành công.");
    }
}

public record GetDisputeByIdQuery(Guid Id) : IRequest<ApiResponse<DisputeTicketDto>>;

public class GetDisputeByIdQueryHandler : IRequestHandler<GetDisputeByIdQuery, ApiResponse<DisputeTicketDto>>
{
    private readonly IDisputeTicketRepository _disputeRepository;

    public GetDisputeByIdQueryHandler(IDisputeTicketRepository disputeRepository)
    {
        _disputeRepository = disputeRepository;
    }

    public async Task<ApiResponse<DisputeTicketDto>> Handle(GetDisputeByIdQuery request, CancellationToken cancellationToken)
    {
        var ticket = await _disputeRepository.GetByIdAsync(request.Id, cancellationToken);
        if (ticket == null)
        {
            throw new NotFoundException($"Không tìm thấy khiếu nại với ID '{request.Id}'.");
        }

        var dto = new DisputeTicketDto
        {
            Id = ticket.Id,
            TradeTransactionId = ticket.TradeTransactionId,
            ComplainantId = ticket.ComplainantId,
            ComplainantName = ticket.Complainant?.UserProfile?.FullName ?? ticket.Complainant?.UserName ?? string.Empty,
            Reason = ticket.Reason,
            Status = ticket.Status,
            ResolvedByAdminId = ticket.ResolvedByAdminId,
            AdminResolutionNotes = ticket.AdminResolutionNotes,
            CreatedDate = ticket.CreatedDate
        };

        return ApiResponse<DisputeTicketDto>.SuccessResult(dto, "Lấy thông tin khiếu nại thành công.");
    }
}
