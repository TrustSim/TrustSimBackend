using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Trade.Queries;

public class GetMyTradeTransactionsQueryHandler : IRequestHandler<GetMyTradeTransactionsQuery, PagedResult<TradeTransactionDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetMyTradeTransactionsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<TradeTransactionDto>> Handle(GetMyTradeTransactionsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _unitOfWork.TradeTransactions.GetPagedByUserIdAsync(
            request.UserId,
            request.Status,
            request.PageIndex,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(t => new TradeTransactionDto
        {
            Id = t.Id,
            TradeTransactionCode = t.TradeTransactionCode,
            AuctionId = t.AuctionId,
            BuyerId = t.BuyerId,
            BuyerName = t.Buyer?.UserProfile?.FullName ?? t.Buyer?.UserName ?? string.Empty,
            SellerId = t.SellerId,
            SellerName = t.Seller?.UserProfile?.FullName ?? t.Seller?.UserName ?? string.Empty,
            SimNumberId = t.SimNumberId,
            SimPhoneNumber = t.SimNumber?.PhoneNumber ?? string.Empty,
            EscrowAmount = t.Escrow?.EscrowAmount ?? 0,
            Status = t.TradeTransactionStatus,
            EscrowStatus = t.Escrow?.EscrowStatus ?? EscrowStatus.PENDING_FUNDING,
            EscrowDeadline = t.EscrowDeadline,
            CreatedDate = t.CreatedDate
        }).ToList();

        return PagedResult<TradeTransactionDto>.Create(dtos, totalCount, request.PageIndex, request.PageSize);
    }
}
