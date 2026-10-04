using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands;

public class RejectRunnerUpOfferCommandHandler : IRequestHandler<RejectRunnerUpOfferCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuctionRealtimeNotifier _realtimeNotifier;
    private readonly IAuctionDelayQueueService _delayQueue;

    public RejectRunnerUpOfferCommandHandler(
        IUnitOfWork unitOfWork,
        IAuctionRealtimeNotifier realtimeNotifier,
        IAuctionDelayQueueService delayQueue)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
        _delayQueue = delayQueue;
    }

    public async Task<bool> Handle(RejectRunnerUpOfferCommand request, CancellationToken cancellationToken)
    {
        var auction = await _unitOfWork.Auctions.GetByIdAsync(request.AuctionId, cancellationToken);
        if (auction == null)
        {
            throw new NotFoundException($"Không tìm thấy phiên đấu giá với ID '{request.AuctionId}'.");
        }

        var participant = await _unitOfWork.Auctions.GetParticipantAsync(request.AuctionId, request.UserId, cancellationToken);
        if (participant == null || participant.Status != ParticipantStatus.RUNNER_UP)
        {
            throw new ForbiddenAccessException("Bạn không phải là Người về nhì trong phiên đấu giá này.");
        }

        // Hủy deadline trong Delay Queue
        await _delayQueue.CancelJobAsync(auction.Id, AuctionDelayJobTypes.RUNNER_UP_DEADLINE, cancellationToken);

        // Trả SIM về AVAILABLE
        var sim = await _unitOfWork.SimNumbers.GetByIdAsync(auction.SimNumberId, cancellationToken);
        if (sim != null)
        {
            sim.SimStatus = SimStatus.AVAILABLE;
            _unitOfWork.SimNumbers.Update(sim);
        }

        var now = DateTime.UtcNow;
        var auctionResult = await _unitOfWork.Auctions.GetResultByAuctionIdAsync(auction.Id, cancellationToken);
        if (auctionResult != null)
        {
            auctionResult.AuctionResultStatus = AuctionResultStatus.FAILED;
            auctionResult.CancelledAt = now;
            auctionResult.CancelledReason = "Người về nhì đã từ chối nhận quyền mua SIM.";
            _unitOfWork.Auctions.UpdateResult(auctionResult);
        }

        participant.Status = ParticipantStatus.LOST;
        _unitOfWork.Auctions.UpdateParticipant(participant);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _realtimeNotifier.NotifyAuctionFailedAsync(
            auction.Id,
            "Người về nhì đã từ chối nhận quyền mua SIM. Phiên đấu giá kết thúc thất bại.",
            cancellationToken);

        return true;
    }
}
