using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Trade.DTOs;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Trade.Queries;

public record GetMyTradeTransactionsQuery(
    Guid UserId,
    TradeTransactionStatus? Status = null,
    int PageIndex = 1,
    int PageSize = 20
) : IRequest<ApiResponse<PagedResult<TradeTransactionDto>>>;

public class GetMyTradeTransactionsQueryHandler : IRequestHandler<GetMyTradeTransactionsQuery, ApiResponse<PagedResult<TradeTransactionDto>>>
{
    private readonly ITradeTransactionRepository _tradeRepository;

    public GetMyTradeTransactionsQueryHandler(ITradeTransactionRepository tradeRepository)
    {
        _tradeRepository = tradeRepository;
    }

    public async Task<ApiResponse<PagedResult<TradeTransactionDto>>> Handle(GetMyTradeTransactionsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _tradeRepository.GetPagedByUserIdAsync(
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

        var pagedResult = PagedResult<TradeTransactionDto>.Create(dtos, totalCount, request.PageIndex, request.PageSize);
        return ApiResponse<PagedResult<TradeTransactionDto>>.SuccessResult(pagedResult, "Lấy danh sách giao dịch thành công.");
    }
}

public record GetTradeTransactionByIdQuery(Guid Id, Guid UserId) : IRequest<ApiResponse<TradeTransactionDto>>;

public class GetTradeTransactionByIdQueryHandler : IRequestHandler<GetTradeTransactionByIdQuery, ApiResponse<TradeTransactionDto>>
{
    private readonly ITradeTransactionRepository _tradeRepository;

    public GetTradeTransactionByIdQueryHandler(ITradeTransactionRepository tradeRepository)
    {
        _tradeRepository = tradeRepository;
    }

    public async Task<ApiResponse<TradeTransactionDto>> Handle(GetTradeTransactionByIdQuery request, CancellationToken cancellationToken)
    {
        var trade = await _tradeRepository.GetByIdAsync(request.Id, cancellationToken);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch với ID '{request.Id}'.");
        }

        if (trade.BuyerId != request.UserId && trade.SellerId != request.UserId)
        {
            throw new ForbiddenAccessException("Bạn không có quyền xem thông tin giao dịch này.");
        }

        var dto = new TradeTransactionDto
        {
            Id = trade.Id,
            TradeTransactionCode = trade.TradeTransactionCode,
            AuctionId = trade.AuctionId,
            BuyerId = trade.BuyerId,
            BuyerName = trade.Buyer?.UserProfile?.FullName ?? trade.Buyer?.UserName ?? string.Empty,
            SellerId = trade.SellerId,
            SellerName = trade.Seller?.UserProfile?.FullName ?? trade.Seller?.UserName ?? string.Empty,
            SimNumberId = trade.SimNumberId,
            SimPhoneNumber = trade.SimNumber?.PhoneNumber ?? string.Empty,
            EscrowAmount = trade.Escrow?.EscrowAmount ?? 0,
            Status = trade.TradeTransactionStatus,
            EscrowStatus = trade.Escrow?.EscrowStatus ?? EscrowStatus.PENDING_FUNDING,
            EscrowDeadline = trade.EscrowDeadline,
            CreatedDate = trade.CreatedDate
        };

        return ApiResponse<TradeTransactionDto>.SuccessResult(dto, "Lấy thông tin giao dịch thành công.");
    }
}
