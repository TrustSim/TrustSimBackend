using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Trade.Commands;

public class CreateTradeTransactionCommandHandler : IRequestHandler<CreateTradeTransactionCommand, TradeTransactionDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateTradeTransactionCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<TradeTransactionDto> Handle(CreateTradeTransactionCommand request, CancellationToken cancellationToken)
    {
        if (request.BuyerId == request.SellerId)
        {
            throw new BadRequestException("Người mua và người bán không được trùng nhau.");
        }

        var sim = await _unitOfWork.SimNumbers.GetByIdAsync(request.SimNumberId, cancellationToken);
        if (sim == null)
        {
            throw new NotFoundException($"Không tìm thấy số SIM với ID '{request.SimNumberId}'.");
        }

        var code = $"TRD-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        var trade = new TradeTransaction
        {
            Id = Guid.NewGuid(),
            TradeTransactionCode = code,
            BuyerId = request.BuyerId,
            SellerId = request.SellerId,
            SimNumberId = request.SimNumberId,
            AuctionId = request.AuctionId,
            TradeTransactionStatus = TradeTransactionStatus.PendingEscrow,
            EscrowDeadline = DateTime.UtcNow.AddDays(2),
            SigningDeadline = null,
            TransferDeadline = null,
            LiquidationDeadline = null,
            CompletedAt = null,
            CancelledAt = null
        };

        var escrow = new Escrow
        {
            Id = Guid.NewGuid(),
            TradeTransactionId = trade.Id,
            EscrowAmount = request.EscrowAmount,
            EscrowStatus = EscrowStatus.PENDING_FUNDING,
            HeldAt = DateTime.UtcNow
        };
        trade.Escrow = escrow;

        sim.SimStatus = SimStatus.LISTED;

        await _unitOfWork.TradeTransactions.AddAsync(trade, cancellationToken);
        _unitOfWork.SimNumbers.Update(sim);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new TradeTransactionDto
        {
            Id = trade.Id,
            TradeTransactionCode = trade.TradeTransactionCode,
            AuctionId = trade.AuctionId,
            BuyerId = trade.BuyerId,
            SellerId = trade.SellerId,
            SimNumberId = trade.SimNumberId,
            SimPhoneNumber = sim.PhoneNumber,
            EscrowAmount = escrow.EscrowAmount,
            Status = trade.TradeTransactionStatus,
            EscrowStatus = escrow.EscrowStatus,
            EscrowDeadline = trade.EscrowDeadline,
            CreatedDate = trade.CreatedDate
        };
    }
}
