using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Trade.Queries;

/// <summary>
/// Handler xử lý lấy thông tin chi tiết tiến trình bàn giao SIM và giải ngân Escrow.
/// </summary>
public class GetSimTransferDetailsQueryHandler : IRequestHandler<GetSimTransferDetailsQuery, SimTransferDetailsDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetSimTransferDetailsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<SimTransferDetailsDto> Handle(GetSimTransferDetailsQuery request, CancellationToken cancellationToken)
    {
        var trade = await _unitOfWork.TradeTransactions.GetByIdAsync(request.TradeId, cancellationToken);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch chuyển nhượng có ID '{request.TradeId}'.");
        }

        var isBuyer = trade.BuyerId == request.UserId;
        var isSeller = trade.SellerId == request.UserId;

        if (!isBuyer && !isSeller)
        {
            throw new ForbiddenAccessException("Bạn không có quyền truy cập thông tin bàn giao SIM của giao dịch này.");
        }

        // Tự động kiểm tra và khởi tạo SimTransfer nếu cả 2 bên đã hoàn tất ký hợp đồng
        var buyerSig = trade.TransferContract?.DigitalSignatures?.FirstOrDefault(s => s.SignatureRole == SignatureRole.BUYER);
        var sellerSig = trade.TransferContract?.DigitalSignatures?.FirstOrDefault(s => s.SignatureRole == SignatureRole.SELLER);
        var isFullySigned = buyerSig != null && (sellerSig != null || (trade.TransferContract?.IsAutoEnforced ?? false));

        if (isFullySigned && trade.SimTransfer == null)
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

            if (trade.TransferContract != null && trade.TransferContract.ContractStatus == ContractStatus.DRAFT)
            {
                trade.TransferContract.ContractStatus = ContractStatus.FULLY_SIGNED;
            }

            if (trade.TradeTransactionStatus == TradeTransactionStatus.PendingSignature)
            {
                trade.TradeTransactionStatus = TradeTransactionStatus.PendingTransfer;
            }

            await _unitOfWork.TradeTransactions.AddSimTransferAsync(newSimTransfer, cancellationToken);
            trade.SimTransfer = newSimTransfer;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var simTransfer = trade.SimTransfer;
        var proofs = simTransfer?.TransferProofs?
            .OrderByDescending(p => p.CreatedDate)
            .Select(p => new TransferProofDto(
                p.Id,
                p.ImageUrl,
                p.Description,
                p.ProofStatus.ToString(),
                p.VerifiedAt,
                p.CreatedDate
            )).ToList() ?? new List<TransferProofDto>();

        var escrowAmount = trade.Escrow?.EscrowAmount ?? 0m;
        var platformFeePercent = 0.05m;
        var platformFeeAmount = Math.Round(escrowAmount * platformFeePercent, 2);
        var netSellerPayout = escrowAmount - platformFeeAmount;

        var transferStatus = simTransfer?.SimTransferStatus ?? SimTransferStatus.PENDING_SELLER_ACTION;
        var canSubmitProof = isSeller && (transferStatus == SimTransferStatus.PENDING_SELLER_ACTION || transferStatus == SimTransferStatus.SELLER_PROOF_SUBMITTED);
        var canConfirmTransfer = isBuyer && (transferStatus == SimTransferStatus.SELLER_PROOF_SUBMITTED || transferStatus == SimTransferStatus.PENDING_BUYER_ACTION);

        return new SimTransferDetailsDto(
            TradeId: trade.Id,
            TradeTransactionCode: trade.TradeTransactionCode,
            SimTransferId: simTransfer?.Id,
            SimTransferStatus: transferStatus.ToString(),
            StartAt: simTransfer?.StartAt,
            Deadline: simTransfer?.Deadline,
            CancelledReason: simTransfer?.CancelledReason,
            IsSeller: isSeller,
            IsBuyer: isBuyer,
            CanSubmitProof: canSubmitProof,
            CanConfirmTransfer: canConfirmTransfer,
            SimPhoneNumber: trade.SimNumber?.PhoneNumber ?? "Chưa xác định",
            Proofs: proofs,
            EscrowAmount: escrowAmount,
            EscrowStatus: (trade.Escrow?.EscrowStatus ?? EscrowStatus.HELD).ToString(),
            PlatformFeePercent: platformFeePercent,
            PlatformFeeAmount: platformFeeAmount,
            NetSellerPayout: netSellerPayout,
            CompletedAt: trade.TradeTransactionStatus == TradeTransactionStatus.Completed ? trade.CompletedAt : null,
            LiquidationDocumentUrl: trade.TransferContract?.LiquidationDocumentUrl,
            Message: "Lấy thông tin tiến trình bàn giao SIM thành công."
        );
    }
}
