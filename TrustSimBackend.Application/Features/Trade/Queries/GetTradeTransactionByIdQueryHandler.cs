using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Trade.Queries;

public class GetTradeTransactionByIdQueryHandler : IRequestHandler<GetTradeTransactionByIdQuery, TradeTransactionDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetTradeTransactionByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<TradeTransactionDto> Handle(GetTradeTransactionByIdQuery request, CancellationToken cancellationToken)
    {
        var trade = await _unitOfWork.TradeTransactions.GetByIdAsync(request.Id, cancellationToken);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch với ID '{request.Id}'.");
        }

        if (trade.BuyerId != request.UserId && trade.SellerId != request.UserId)
        {
            throw new ForbiddenAccessException("Bạn không có quyền xem thông tin giao dịch này.");
        }

        return new TradeTransactionDto
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
    }
}
