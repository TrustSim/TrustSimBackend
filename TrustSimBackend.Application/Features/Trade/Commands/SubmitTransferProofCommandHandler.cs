using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Trade.Commands;

/// <summary>
/// Handler xử lý nộp ảnh biên bản chuyển nhượng/sang tên SIM từ nhà mạng của Bên Bán.
/// </summary>
public class SubmitTransferProofCommandHandler : IRequestHandler<SubmitTransferProofCommand, SimTransferDetailsDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly ILogger<SubmitTransferProofCommandHandler> _logger;

    public SubmitTransferProofCommandHandler(
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        ILogger<SubmitTransferProofCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<SimTransferDetailsDto> Handle(SubmitTransferProofCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            throw new BadRequestException("Vui lòng cung cấp đường dẫn hoặc hình ảnh biên bản sang tên SIM.");
        }

        var trade = await _unitOfWork.TradeTransactions.GetByIdAsync(request.TradeId, cancellationToken);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch chuyển nhượng có ID '{request.TradeId}'.");
        }

        if (trade.SellerId != request.UserId)
        {
            throw new ForbiddenAccessException("Chỉ có Người Bán mới có quyền nộp biên bản sang tên SIM.");
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

        var proof = new TransferProof
        {
            Id = Guid.NewGuid(),
            SimTransferId = trade.SimTransfer.Id,
            ImageUrl = request.ImageUrl.Trim(),
            Description = request.Description?.Trim(),
            ProofStatus = ProofStatus.PENDING,
            VerifiedAt = DateTime.UtcNow
        };

        await _unitOfWork.TradeTransactions.AddTransferProofAsync(proof, cancellationToken);
        trade.SimTransfer.AddTransferProof(proof);
        trade.SimTransfer.SimTransferStatus = SimTransferStatus.SELLER_PROOF_SUBMITTED;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[SimTransfer] Người bán {SellerId} đã nộp biên bản sang tên cho SIM {SimNumber} (Giao dịch: {TradeCode})",
            trade.SellerId, trade.SimNumber?.PhoneNumber, trade.TradeTransactionCode);

        // Bắn thông báo cho người mua để kiểm tra và xác nhận
        await _notificationService.SendNotificationAsync(
            trade.BuyerId,
            NotificationType.TRADE_TRANSFERRING_SIM,
            "Người bán đã nộp biên bản sang tên SIM",
            $"Người bán đã nộp biên bản sang tên cho SIM {trade.SimNumber?.PhoneNumber}. Vui lòng kiểm tra sóng và xác nhận nhận SIM.",
            $"/trade/{trade.Id}",
            cancellationToken: cancellationToken);

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
                p.CreatedDate
            )).ToList();

        return new SimTransferDetailsDto(
            TradeId: trade.Id,
            TradeTransactionCode: trade.TradeTransactionCode,
            SimTransferId: trade.SimTransfer.Id,
            SimTransferStatus: trade.SimTransfer.SimTransferStatus.ToString(),
            StartAt: trade.SimTransfer.StartAt,
            Deadline: trade.SimTransfer.Deadline,
            CancelledReason: trade.SimTransfer.CancelledReason,
            IsSeller: true,
            IsBuyer: false,
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
            Message: "Đã nộp biên bản sang tên SIM thành công. Hệ thống đã thông báo cho Bên Mua kiểm tra và xác nhận."
        );
    }
}
