using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Trade.Commands;

public class UpdateTradeStatusCommandHandler : IRequestHandler<UpdateTradeStatusCommand, bool>
{
    private readonly ITradeTransactionRepository _tradeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTradeStatusCommandHandler(ITradeTransactionRepository tradeRepository, IUnitOfWork unitOfWork)
    {
        _tradeRepository = tradeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(UpdateTradeStatusCommand request, CancellationToken cancellationToken)
    {
        var trade = await _tradeRepository.GetByIdAsync(request.TradeId, cancellationToken);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch với ID '{request.TradeId}'.");
        }

        trade.TradeTransactionStatus = request.NewStatus;
        _tradeRepository.Update(trade);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
