using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Trade.DTOs;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Trade.Commands;

public record CreateTradeTransactionCommand(
    Guid BuyerId,
    Guid SellerId,
    Guid SimNumberId,
    Guid AuctionId,
    decimal EscrowAmount
) : IRequest<ApiResponse<TradeTransactionDto>>;

public class CreateTradeTransactionCommandHandler : IRequestHandler<CreateTradeTransactionCommand, ApiResponse<TradeTransactionDto>>
{
    private readonly ITradeTransactionRepository _tradeRepository;
    private readonly ISimNumberRepository _simRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateTradeTransactionCommandHandler(
        ITradeTransactionRepository tradeRepository,
        ISimNumberRepository simRepository,
        IUnitOfWork unitOfWork)
    {
        _tradeRepository = tradeRepository;
        _simRepository = simRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<TradeTransactionDto>> Handle(CreateTradeTransactionCommand request, CancellationToken cancellationToken)
    {
        if (request.BuyerId == request.SellerId)
        {
            throw new BadRequestException("Người mua và người bán không được trùng nhau.");
        }

        var sim = await _simRepository.GetByIdAsync(request.SimNumberId, cancellationToken);
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
            SigningDeadline = DateTime.UtcNow.AddDays(4),
            TransferDeadline = DateTime.UtcNow.AddDays(7)
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

        await _tradeRepository.AddAsync(trade, cancellationToken);
        _simRepository.Update(sim);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new TradeTransactionDto
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

        return ApiResponse<TradeTransactionDto>.SuccessResult(dto, "Tạo giao dịch chuyển nhượng SIM thành công.");
    }
}

public record UpdateTradeStatusCommand(
    Guid TradeId,
    TradeTransactionStatus NewStatus
) : IRequest<ApiResponse<bool>>;

public class UpdateTradeStatusCommandHandler : IRequestHandler<UpdateTradeStatusCommand, ApiResponse<bool>>
{
    private readonly ITradeTransactionRepository _tradeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTradeStatusCommandHandler(ITradeTransactionRepository tradeRepository, IUnitOfWork unitOfWork)
    {
        _tradeRepository = tradeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<bool>> Handle(UpdateTradeStatusCommand request, CancellationToken cancellationToken)
    {
        var trade = await _tradeRepository.GetByIdAsync(request.TradeId, cancellationToken);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch với ID '{request.TradeId}'.");
        }

        trade.TradeTransactionStatus = request.NewStatus;
        _tradeRepository.Update(trade);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.SuccessResult(true, "Cập nhật trạng thái giao dịch thành công.");
    }
}
