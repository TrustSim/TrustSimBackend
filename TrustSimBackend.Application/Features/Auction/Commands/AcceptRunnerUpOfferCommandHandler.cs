using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands;

public class AcceptRunnerUpOfferCommandHandler : IRequestHandler<AcceptRunnerUpOfferCommand, AuctionResultDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuctionRealtimeNotifier _realtimeNotifier;
    private readonly IAuctionDelayQueueService _delayQueue;
    private readonly INotificationService _notificationService;

    public AcceptRunnerUpOfferCommandHandler(
        IUnitOfWork unitOfWork,
        IAuctionRealtimeNotifier realtimeNotifier,
        IAuctionDelayQueueService delayQueue,
        INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
        _delayQueue = delayQueue;
        _notificationService = notificationService;
    }

    public async Task<AuctionResultDto> Handle(AcceptRunnerUpOfferCommand request, CancellationToken cancellationToken)
    {
        var auction = await _unitOfWork.Auctions.GetByIdAsync(request.AuctionId, cancellationToken);
        if (auction == null)
        {
            throw new NotFoundException($"Không tìm thấy phiên đấu giá với ID '{request.AuctionId}'.");
        }

        var participant = await _unitOfWork.Auctions.GetParticipantAsync(request.AuctionId, request.UserId, cancellationToken);
        if (participant == null || participant.Status != ParticipantStatus.RUNNER_UP)
        {
            throw new ForbiddenAccessException("Bạn không phải là Người về nhì được nhận quyền mua trong phiên này.");
        }

        var auctionResult = await _unitOfWork.Auctions.GetResultByAuctionIdAsync(auction.Id, cancellationToken);
        if (auctionResult == null || auctionResult.AuctionResultStatus != AuctionResultStatus.RUNNER_UP_PENDING)
        {
            throw new BadRequestException("Phiên đấu giá không ở trạng thái chờ Người về nhì xác nhận mua.");
        }

        var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.UserId, cancellationToken);
        if (wallet == null)
        {
            throw new NotFoundException("Không tìm thấy ví tiền của người dùng.");
        }

        // Lấy danh sách lượt đặt giá của phiên
        var allBids = await _unitOfWork.Bids.GetBidsByAuctionIdAsync(auction.Id, top: 100, cancellationToken);
        var runnerUpBid = allBids.FirstOrDefault(b => b.ParticipantId == participant.Id);

        // Lấy mức giá của Người về nhì từ RunnerUpFinalPrice (mức giá Bid cuối của chính họ, không ép theo giá người thắng)
        decimal priceToPay;
        if (auctionResult.RunnerUpFinalPrice.HasValue && auctionResult.RunnerUpFinalPrice > 0)
        {
            priceToPay = auctionResult.RunnerUpFinalPrice.Value;
        }
        else
        {
            if (runnerUpBid == null)
            {
                throw new BadRequestException("Không tìm thấy lịch sử trả giá của Người về nhì.");
            }
            priceToPay = runnerUpBid.BidAmount;
        }

        var now = DateTime.UtcNow;
        var paymentGraceHours = auction.AuctionRule?.PaymentGracePeriodHours ?? 24;
        var paymentDeadline = now.AddHours(paymentGraceHours);

        // 1. Cập nhật tư cách người tham gia của Người về nhì thành WON
        participant.Status = ParticipantStatus.WON;
        _unitOfWork.Auctions.UpdateParticipant(participant);

        // 2. Cập nhật Bid của Runner-Up thành WINNING
        if (runnerUpBid != null)
        {
            runnerUpBid.Status = BidStatus.WINNING;
            _unitOfWork.Bids.Update(runnerUpBid);
        }

        // 3. Cập nhật giá hiện tại của Auction thành giá mua thực tế của Runner-Up
        auction.CurrentPrice = priceToPay;
        _unitOfWork.Auctions.Update(auction);

        // 4. Cập nhật kết quả đấu giá: Runner-up trở thành người thắng, chuyển sang chờ thanh toán (PENDING_PAYMENT) với hạn chót 24h
        auctionResult.WinnerId = participant.Id;
        auctionResult.FinalPrice = priceToPay;
        auctionResult.WinType = AuctionWinType.RUNNER_UP_TAKEOVER;
        auctionResult.AuctionResultStatus = AuctionResultStatus.PENDING_PAYMENT;
        auctionResult.PaymentDeadline = paymentDeadline;
        auctionResult.RunnerUpPaymentDeadline = paymentDeadline;
        _unitOfWork.Auctions.UpdateResult(auctionResult);

        // 5. Hủy deadline Á quân trong Delay Queue và nạp deadline thanh toán mới
        await _delayQueue.CancelJobAsync(auction.Id, AuctionDelayJobTypes.RUNNER_UP_DEADLINE, cancellationToken);
        await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
        {
            AuctionId = auction.Id,
            JobType = AuctionDelayJobTypes.PAYMENT_DEADLINE,
            ExecuteAt = paymentDeadline,
            ExtraData = participant.Id.ToString()
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 6. Gửi thông báo cho Á quân và Seller
        var simPhone = auction.SimNumber?.PhoneNumber ?? "SIM";

        await _notificationService.SendNotificationAsync(
            userId: participant.UserAccountId,
            type: NotificationType.AUCTION_WON,
            title: $"🎉 Chấp nhận quyền mua SIM {simPhone} thành công!",
            content: $"Bạn đã đồng ý nhận quyền mua SIM {simPhone} với mức giá {priceToPay:N0} VNĐ. Vui lòng nạp tiền vào ví và hoàn tất thanh toán ký quỹ Escrow trước {paymentDeadline:dd/MM/yyyy HH:mm} để tiến hành ký kết hợp đồng.",
            actionUrl: $"/auctions/{auction.Id}/payment",
            referenceType: "Auction",
            referenceId: auction.Id,
            sendEmail: true,
            cancellationToken: cancellationToken);

        await _notificationService.SendNotificationAsync(
            userId: auction.AuthorId,
            type: NotificationType.SYSTEM_ALERT,
            title: $"📢 Người về nhì đã đồng ý mua SIM {simPhone}!",
            content: $"Người về nhì trong phiên {auction.AuctionCode} đã đồng ý mua SIM với mức giá {priceToPay:N0} VNĐ. Người mua có {paymentGraceHours} giờ để hoàn tất nạp tiền và thanh toán ký quỹ.",
            actionUrl: $"/auctions/{auction.Id}",
            referenceType: "Auction",
            referenceId: auction.Id,
            sendEmail: true,
            cancellationToken: cancellationToken);

        var resultDto = new AuctionResultDto
        {
            Id = auctionResult.Id,
            AuctionId = auction.Id,
            WinnerId = participant.Id,
            WinnerUserName = participant.UserAccount?.UserName,
            FinalPrice = priceToPay,
            InitialWinnerId = auctionResult.InitialWinnerId,
            InitialWinningPrice = auctionResult.InitialWinningPrice,
            RunnerUpParticipantId = auctionResult.RunnerUpParticipantId,
            RunnerUpFinalPrice = auctionResult.RunnerUpFinalPrice,
            WinType = AuctionWinType.RUNNER_UP_TAKEOVER,
            AuctionResultStatus = AuctionResultStatus.PENDING_PAYMENT,
            PaymentDeadline = paymentDeadline,
            SettledAt = auctionResult.SettledAt
        };

        await _realtimeNotifier.NotifyAuctionStatusChangedAsync(
            auction.Id,
            AuctionResultStatus.PENDING_PAYMENT.ToString(),
            participant.Id,
            priceToPay,
            cancellationToken);

        return resultDto;
    }
}
