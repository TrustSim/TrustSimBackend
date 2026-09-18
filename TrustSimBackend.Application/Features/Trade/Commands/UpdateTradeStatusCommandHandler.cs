using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Trade.Commands;

public class UpdateTradeStatusCommandHandler : IRequestHandler<UpdateTradeStatusCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTradeStatusCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(UpdateTradeStatusCommand request, CancellationToken cancellationToken)
    {
        var trade = await _unitOfWork.TradeTransactions.GetByIdAsync(request.TradeId, cancellationToken);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch với ID '{request.TradeId}'.");
        }

        trade.TradeTransactionStatus = request.NewStatus;
        _unitOfWork.TradeTransactions.Update(trade);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
