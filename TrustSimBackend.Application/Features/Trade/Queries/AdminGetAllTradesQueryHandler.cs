using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Trade.Queries;

public class AdminGetAllTradesQueryHandler : IRequestHandler<AdminGetAllTradesQuery, PagedResult<TradeTransactionDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public AdminGetAllTradesQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<TradeTransactionDto>> Handle(AdminGetAllTradesQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _unitOfWork.TradeTransactions.GetPagedAsync(
            request.Status,
            request.PageIndex,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(t => new TradeTransactionDto
        {
            Id = t.Id,
            TradeTransactionCode = t.TradeTransactionCode,
            AuctionId = t.AuctionId,
            SimNumberId = t.SimNumberId,
            SimPhoneNumber = t.SimNumber?.PhoneNumber ?? string.Empty,
            BuyerId = t.BuyerId,
            BuyerName = t.Buyer?.UserProfile?.FullName ?? t.Buyer?.UserName ?? string.Empty,
            SellerId = t.SellerId,
            SellerName = t.Seller?.UserProfile?.FullName ?? t.Seller?.UserName ?? string.Empty,
            EscrowAmount = t.Escrow?.EscrowAmount ?? 0m,
            Status = t.TradeTransactionStatus,
            EscrowStatus = t.Escrow?.EscrowStatus ?? EscrowStatus.PENDING_FUNDING,
            EscrowDeadline = t.EscrowDeadline,
            CreatedDate = t.CreatedDate
        }).ToList();

        return new PagedResult<TradeTransactionDto>(dtos, totalCount, request.PageIndex, request.PageSize);
    }
}
