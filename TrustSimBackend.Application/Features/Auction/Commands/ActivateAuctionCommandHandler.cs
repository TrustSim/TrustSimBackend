using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Application.Features.Auction.States;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands;

public class ActivateAuctionCommandHandler : IRequestHandler<ActivateAuctionCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuctionRealtimeNotifier _realtimeNotifier;

    public ActivateAuctionCommandHandler(
        IUnitOfWork unitOfWork,
        IAuctionRealtimeNotifier realtimeNotifier)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<bool> Handle(ActivateAuctionCommand request, CancellationToken cancellationToken)
    {
        var auction = await _unitOfWork.Auctions.GetByIdAsync(request.AuctionId, cancellationToken);
        if (auction == null)
        {
            throw new NotFoundException($"Không tìm thấy phiên đấu giá với ID '{request.AuctionId}'.");
        }

        if (auction.Participants.Count == 0)
        {
            throw new BadRequestException("Không thể kích hoạt phiên đấu giá khi chưa có người tham gia đặt cọc.");
        }

        var state = AuctionStateContext.GetState(auction.AuctionStatus);
        state.Activate(auction);

        _unitOfWork.Auctions.Update(auction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _realtimeNotifier.NotifyAuctionStatusChangedAsync(
            auction.Id,
            auction.AuctionStatus.ToString(),
            null,
            null,
            cancellationToken);

        return true;
    }
}
