using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Application.Features.Trade.Helpers;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Trade.Commands;

/// <summary>
/// Handler xử lý thu hồi (hủy) bộ bằng chứng đang PENDING để người dùng có thể tải lên bộ bằng chứng thay thế.
/// </summary>
public class WithdrawTransferProofCommandHandler : IRequestHandler<WithdrawTransferProofCommand, SimTransferDetailsDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuctionDelayQueueService _delayQueue;
    private readonly INotificationService? _notificationService;
    private readonly ILogger<WithdrawTransferProofCommandHandler> _logger;

    public WithdrawTransferProofCommandHandler(
        IUnitOfWork unitOfWork,
        IAuctionDelayQueueService delayQueue,
        ILogger<WithdrawTransferProofCommandHandler> logger,
        INotificationService? notificationService = null)
    {
        _unitOfWork = unitOfWork;
        _delayQueue = delayQueue;
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task<SimTransferDetailsDto> Handle(WithdrawTransferProofCommand request, CancellationToken cancellationToken)
    {
        var proof = await _unitOfWork.TradeTransactions.GetTransferProofByIdAsync(request.TransferProofId, cancellationToken);
        if (proof == null)
        {
            throw new NotFoundException($"Không tìm thấy bộ bằng chứng có ID '{request.TransferProofId}'.");
        }

        var trade = await _unitOfWork.TradeTransactions.GetByAuctionIdAsync(proof.AuctionId, cancellationToken);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch chuyển nhượng cho phiên đấu giá '{proof.AuctionId}'.");
        }

        var isAuthorizedSubmitter = proof.SubmittedByUserId == request.UserId
            || (proof.ProofType == TransferProofType.SELLER_HANDOVER && trade.SellerId == request.UserId)
            || (proof.ProofType == TransferProofType.BUYER_RECEIPT && trade.BuyerId == request.UserId);

        if (!isAuthorizedSubmitter)
        {
            throw new ForbiddenAccessException("Bạn không có quyền thu hồi bộ bằng chứng này.");
        }

        if (trade.SimTransfer == null)
        {
            throw new BadRequestException("Tiến trình bàn giao SIM chưa được khởi tạo cho giao dịch này.");
        }

        if (trade.SimTransfer.SimTransferStatus == SimTransferStatus.COMPLETED)
        {
            throw new BadRequestException("Giao dịch chuyển nhượng SIM đã hoàn tất, không thể thu hồi bằng chứng.");
        }

        if (trade.SimTransfer.SimTransferStatus == SimTransferStatus.CANCELLED)
        {
            throw new BadRequestException("Giao dịch chuyển nhượng SIM đã bị hủy bỏ.");
        }

        if (proof.ProofStatus != ProofStatus.PENDING)
        {
            throw new BadRequestException($"Chỉ có thể thu hồi bộ bằng chứng đang chờ duyệt (PENDING). Trạng thái hiện tại: {proof.ProofStatus}.");
        }

        var now = DateTime.UtcNow;
        proof.ProofStatus = ProofStatus.WITHDRAWN;
        proof.WithdrawnAt = now;

        if (proof.ProofType == TransferProofType.SELLER_HANDOVER)
        {
            trade.SimTransfer.SimTransferStatus = SimTransferStatus.PENDING_SELLER_ACTION;
            trade.LiquidationDeadline = null;
            await _delayQueue.CancelJobAsync(trade.AuctionId, AuctionDelayJobTypes.LIQUIDATION_DEADLINE, cancellationToken);
        }
        else
        {
            trade.SimTransfer.SimTransferStatus = SimTransferStatus.PENDING_BUYER_ACTION;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[SimTransfer] Người dùng {UserId} đã thu hồi bộ bằng chứng {ProofId} ({ProofType}) của giao dịch {TradeCode}",
            request.UserId, proof.Id, proof.ProofType, trade.TradeTransactionCode);

        if (_notificationService != null)
        {
            try
            {
                var isSeller = proof.ProofType == TransferProofType.SELLER_HANDOVER;
                var counterpartyId = isSeller ? trade.BuyerId : trade.SellerId;
                var phone = trade.SimNumber?.PhoneNumber;
                var metadata = new
                {
                    tradeId = trade.Id,
                    tradeCode = trade.TradeTransactionCode,
                    auctionId = trade.AuctionId,
                    simNumber = phone,
                    proofId = proof.Id,
                    proofType = proof.ProofType.ToString(),
                    stage = isSeller ? "SELLER_PROOF_WITHDRAWN" : "BUYER_PROOF_WITHDRAWN"
                };

                var partyTitle = isSeller
                    ? "Người bán đã thu hồi biên bản sang tên SIM"
                    : "Người mua đã thu hồi ảnh nghiệm thu SIM";
                var partyContent = isSeller
                    ? $"Người bán đã thu hồi bộ bằng chứng sang tên đang chờ duyệt cho SIM {phone} (Mã GD: {trade.TradeTransactionCode}) để cập nhật lại hồ sơ."
                    : $"Người mua đã thu hồi bộ ảnh nghiệm thu đang chờ duyệt cho SIM {phone} (Mã GD: {trade.TradeTransactionCode}) để tải lại ảnh mới.";

                await _notificationService.SendNotificationAsync(
                    counterpartyId,
                    NotificationType.TRANSFER_PROOF_WITHDRAWN,
                    partyTitle,
                    partyContent,
                    actionUrl: $"/trade/{trade.Id}",
                    referenceType: "TradeTransaction",
                    referenceId: trade.Id,
                    metadata: metadata,
                    sendEmail: false,
                    cancellationToken: cancellationToken);

                await _notificationService.SendNotificationToRolesAsync(
                    new[] { UserRole.ADMIN, UserRole.CONSULTANT },
                    NotificationType.TRANSFER_PROOF_WITHDRAWN,
                    "Bộ bằng chứng chờ duyệt đã được thu hồi",
                    $"{(isSeller ? "Người bán" : "Người mua")} đã thu hồi bộ bằng chứng chờ duyệt của SIM {phone} (Mã GD: {trade.TradeTransactionCode}).",
                    actionUrl: $"/transfer-review?tradeId={trade.Id}",
                    referenceType: "TradeTransaction",
                    referenceId: trade.Id,
                    metadata: metadata,
                    sendEmail: false,
                    excludeUserId: request.UserId,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "[WithdrawProof] Lỗi khi gửi thông báo thu hồi bằng chứng cho Trade {TradeCode}", trade.TradeTransactionCode);
            }
        }

        return SimTransferMappingHelper.BuildSimTransferDetailsDto(
            trade,
            request.UserId,
            isAdmin: false,
            message: "Đã thu hồi bộ bằng chứng đang chờ thành công. Bạn có thể tải lên bộ bằng chứng mới.");
    }
}
