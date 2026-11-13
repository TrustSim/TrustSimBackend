using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Trade.Commands;

/// <summary>
/// Handler xử lý nộp nhiều ảnh bằng chứng chuyển nhượng SIM (Bên Bán hoặc Bên Mua).
/// </summary>
public class SubmitMultipleTransferProofsCommandHandler : IRequestHandler<SubmitMultipleTransferProofsCommand, SimTransferDetailsDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICloudStorageService _cloudStorageService;
    private readonly INotificationService _notificationService;
    private readonly IAuctionDelayQueueService _delayQueue;
    private readonly TradeDeadlineSettings _deadlineSettings;
    private readonly ILogger<SubmitMultipleTransferProofsCommandHandler> _logger;

    public SubmitMultipleTransferProofsCommandHandler(
        IUnitOfWork unitOfWork,
        ICloudStorageService cloudStorageService,
        INotificationService notificationService,
        IAuctionDelayQueueService delayQueue,
        IOptions<TradeDeadlineSettings> deadlineOptions,
        ILogger<SubmitMultipleTransferProofsCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _cloudStorageService = cloudStorageService;
        _notificationService = notificationService;
        _delayQueue = delayQueue;
        _deadlineSettings = deadlineOptions.Value;
        _logger = logger;
    }

    public async Task<SimTransferDetailsDto> Handle(SubmitMultipleTransferProofsCommand request, CancellationToken cancellationToken)
    {
        if (request.Files == null || request.Files.Count == 0)
        {
            throw new BadRequestException("Vui lòng tải lên ít nhất một hình ảnh bằng chứng.");
        }

        var trade = await _unitOfWork.TradeTransactions.GetByIdAsync(request.TradeId, cancellationToken);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch chuyển nhượng có ID '{request.TradeId}'.");
        }

        var isSeller = request.ProofType == TransferProofType.SELLER_HANDOVER;

        if (isSeller)
        {
            if (trade.SellerId != request.UserId)
            {
                throw new ForbiddenAccessException("Chỉ có Người Bán mới có quyền nộp biên bản sang tên SIM.");
            }
        }
        else
        {
            if (trade.BuyerId != request.UserId)
            {
                throw new ForbiddenAccessException("Chỉ có Người Mua mới có quyền nộp bằng chứng nghiệm thu SIM.");
            }
        }

        // Đảm bảo SimTransfer tồn tại
        if (trade.SimTransfer == null)
        {
            var now = DateTime.UtcNow;
            var newSimTransfer = new SimTransfer
            {
                Id = Guid.NewGuid(),
                TradeTransactionId = trade.Id,
                StartAt = now,
                Deadline = now.AddDays(7),
                SimTransferStatus = SimTransferStatus.PENDING_SELLER_ACTION
            };
            await _unitOfWork.TradeTransactions.AddSimTransferAsync(newSimTransfer, cancellationToken);
            trade.SimTransfer = newSimTransfer;
        }

        if (trade.SimTransfer.SimTransferStatus == SimTransferStatus.COMPLETED)
        {
            throw new BadRequestException("Giao dịch chuyển nhượng SIM này đã hoàn tất thành công trước đó.");
        }

        if (trade.SimTransfer.SimTransferStatus == SimTransferStatus.CANCELLED)
        {
            throw new BadRequestException("Giao dịch chuyển nhượng SIM này đã bị hủy bỏ.");
        }

        var hasApprovedSellerProof = trade.SimTransfer.TransferProofs
            .Any(p => p.ProofType == TransferProofType.SELLER_HANDOVER && p.ProofStatus == ProofStatus.APPROVED);

        if (isSeller)
        {
            if (hasApprovedSellerProof)
            {
                throw new BadRequestException("Bằng chứng sang tên của Người Bán đã được Ban Quản Trị phê duyệt, không thể nộp lại.");
            }
        }
        else
        {
            if (!hasApprovedSellerProof)
            {
                throw new BadRequestException("Bằng chứng chuyển nhượng của Người Bán phải được Ban Quản Trị sàn phê duyệt thành công trước khi Người Mua nộp bằng chứng nhận SIM.");
            }
        }

        // Tải lên từng file và thêm TransferProof
        foreach (var file in request.Files)
        {
            if (file.Length == 0) continue;

            var uploadedUrl = await _cloudStorageService.UploadImageAsync(file, "sim-transfer", cancellationToken);

            var proof = new TransferProof
            {
                Id = Guid.NewGuid(),
                SimTransferId = trade.SimTransfer.Id,
                ImageUrl = uploadedUrl,
                Description = request.Description?.Trim(),
                ProofStatus = ProofStatus.PENDING,
                ProofType = request.ProofType,
                VerifiedAt = DateTime.UtcNow
            };

            await _unitOfWork.TradeTransactions.AddTransferProofAsync(proof, cancellationToken);
            trade.SimTransfer.AddTransferProof(proof);
        }

        if (isSeller)
        {
            trade.SimTransfer.SimTransferStatus = SimTransferStatus.SELLER_PROOF_SUBMITTED;

            // Kích hoạt mốc thời hạn Nghiệm thu
            var liqHours = _deadlineSettings.LiquidationSigningDeadlineHours > 0
                ? _deadlineSettings.LiquidationSigningDeadlineHours
                : 72;
            var liquidationDeadline = DateTime.UtcNow.AddHours(liqHours);
            trade.LiquidationDeadline = liquidationDeadline;

            await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
            {
                AuctionId = trade.AuctionId,
                JobType = AuctionDelayJobTypes.LIQUIDATION_DEADLINE,
                ExecuteAt = liquidationDeadline,
                ExtraData = trade.Id.ToString()
            }, cancellationToken);

            _logger.LogInformation(
                "[SimTransfer] Người bán {SellerId} đã nộp {Count} ảnh biên bản sang tên cho SIM {SimNumber} (Giao dịch: {TradeCode})",
                trade.SellerId, request.Files.Count, trade.SimNumber?.PhoneNumber, trade.TradeTransactionCode);

            await _notificationService.SendNotificationAsync(
                trade.BuyerId,
                NotificationType.TRADE_TRANSFERRING_SIM,
                "Người bán đã nộp biên bản sang tên SIM",
                $"Người bán đã nộp {request.Files.Count} ảnh biên bản sang tên cho SIM {trade.SimNumber?.PhoneNumber}. Vui lòng chờ Ban Quản Trị sàn kiểm tra và duyệt hồ sơ.",
                $"/trade/{trade.Id}",
                cancellationToken: cancellationToken);
        }
        else
        {
            trade.SimTransfer.SimTransferStatus = SimTransferStatus.BUYER_PROOF_SUBMITTED;

            _logger.LogInformation(
                "[SimTransfer] Người mua {BuyerId} đã nộp {Count} ảnh nghiệm thu SIM {SimNumber} (Giao dịch: {TradeCode})",
                trade.BuyerId, request.Files.Count, trade.SimNumber?.PhoneNumber, trade.TradeTransactionCode);

            await _notificationService.SendNotificationAsync(
                trade.SellerId,
                NotificationType.TRADE_TRANSFERRING_SIM,
                "Người mua đã nộp ảnh nghiệm thu SIM",
                $"Người mua đã nộp {request.Files.Count} ảnh kiểm tra sóng / nghiệm thu cho SIM {trade.SimNumber?.PhoneNumber}. Đang chờ Ban Quản Trị sàn duyệt hoàn tất và giải ngân.",
                $"/trade/{trade.Id}",
                cancellationToken: cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var escrowAmount = trade.Escrow?.EscrowAmount ?? 0m;
        var platformFeePercent = 0.05m;
        var platformFeeAmount = Math.Round(escrowAmount * platformFeePercent, 2);
        var netSellerPayout = escrowAmount - platformFeeAmount;

        var proofs = trade.SimTransfer.TransferProofs
            .OrderByDescending(p => p.CreatedDate)
            .Select(p => new TransferProofDto(
                p.Id,
                p.ImageUrl,
                p.Description,
                p.ProofStatus.ToString(),
                p.VerifiedAt,
                p.CreatedDate,
                p.ProofType.ToString()
            )).ToList();

        var message = isSeller
            ? $"Đã tải lên {request.Files.Count} ảnh biên bản sang tên thành công. Chờ Admin sàn duyệt hồ sơ."
            : $"Đã tải lên {request.Files.Count} ảnh kiểm tra nghiệm thu thành công. Chờ Admin sàn duyệt và giải ngân.";

        return new SimTransferDetailsDto(
            TradeId: trade.Id,
            TradeTransactionCode: trade.TradeTransactionCode,
            SimTransferId: trade.SimTransfer.Id,
            SimTransferStatus: trade.SimTransfer.SimTransferStatus.ToString(),
            StartAt: trade.SimTransfer.StartAt,
            Deadline: trade.SimTransfer.Deadline,
            CancelledReason: trade.SimTransfer.CancelledReason,
            IsSeller: trade.SellerId == request.UserId,
            IsBuyer: trade.BuyerId == request.UserId,
            CanSubmitProof: true,
            CanConfirmTransfer: false,
            SimPhoneNumber: trade.SimNumber?.PhoneNumber ?? "Chưa xác định",
            Proofs: proofs,
            EscrowAmount: escrowAmount,
            EscrowStatus: (trade.Escrow?.EscrowStatus ?? EscrowStatus.HELD).ToString(),
            PlatformFeePercent: platformFeePercent,
            PlatformFeeAmount: platformFeeAmount,
            NetSellerPayout: netSellerPayout,
            CompletedAt: null,
            Message: message
        );
    }
}
