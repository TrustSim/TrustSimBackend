using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Trade.Helpers;

/// <summary>
/// Helper ánh xạ dùng chung cho tiến trình bàn giao SIM và bộ bằng chứng chuyển nhượng.
/// </summary>
public static class SimTransferMappingHelper
{
    public const decimal DefaultPlatformFeePercent = 0.05m;

    /// <summary>
    /// Gom và loại trùng danh sách TransferProof từ cả Auction và SimTransfer (theo Id).
    /// </summary>
    public static List<TransferProof> GetMergedProofs(TradeTransaction trade)
    {
        var byId = new Dictionary<Guid, TransferProof>();

        if (trade.Auction?.TransferProofs != null)
        {
            foreach (var proof in trade.Auction.TransferProofs)
            {
                byId[proof.Id] = proof;
            }
        }

        if (trade.SimTransfer?.TransferProofs != null)
        {
            foreach (var proof in trade.SimTransfer.TransferProofs)
            {
                byId[proof.Id] = proof;
            }
        }

        return byId.Values
            .OrderByDescending(p => p.CreatedDate)
            .ToList();
    }

    public static TransferProofDto MapToTransferProofDto(TransferProof proof)
    {
        var images = proof.Images
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.CreatedDate)
            .Select(i => new TransferProofImageDto(
                i.Id,
                i.ImageUrl,
                i.FileName,
                i.ContentType,
                i.FileSizeBytes,
                i.SortOrder))
            .ToList();

        return new TransferProofDto(
            Id: proof.Id,
            AuctionId: proof.AuctionId,
            SimTransferId: proof.SimTransferId,
            ProofType: proof.ProofType.ToString(),
            ProofStatus: proof.ProofStatus.ToString(),
            Description: proof.Description,
            RejectionReason: proof.RejectionReason,
            SubmittedByUserId: proof.SubmittedByUserId,
            VerifiedByAdminId: proof.VerifiedByAdminId,
            VerifiedAt: proof.VerifiedAt,
            WithdrawnAt: proof.WithdrawnAt,
            CreatedDate: proof.CreatedDate,
            Images: images);
    }

    public static SimTransferDetailsDto BuildSimTransferDetailsDto(
        TradeTransaction trade,
        Guid currentUserId,
        bool isAdmin,
        string? message = null)
    {
        var isSeller = !isAdmin && trade.SellerId == currentUserId;
        var isBuyer = !isAdmin && trade.BuyerId == currentUserId;

        var simTransfer = trade.SimTransfer;
        var transferStatus = simTransfer?.SimTransferStatus ?? SimTransferStatus.PENDING_SELLER_ACTION;

        var mergedProofs = GetMergedProofs(trade);
        var proofDtos = mergedProofs.Select(MapToTransferProofDto).ToList();

        var hasApprovedSellerProof = mergedProofs
            .Any(p => p.ProofType == TransferProofType.SELLER_HANDOVER && p.ProofStatus == ProofStatus.APPROVED);
        var hasApprovedBuyerProof = mergedProofs
            .Any(p => p.ProofType == TransferProofType.BUYER_RECEIPT && p.ProofStatus == ProofStatus.APPROVED);
        var hasPendingSellerProof = mergedProofs
            .Any(p => p.ProofType == TransferProofType.SELLER_HANDOVER && p.ProofStatus == ProofStatus.PENDING);
        var hasPendingBuyerProof = mergedProofs
            .Any(p => p.ProofType == TransferProofType.BUYER_RECEIPT && p.ProofStatus == ProofStatus.PENDING);

        var isActiveTransfer = transferStatus != SimTransferStatus.COMPLETED && transferStatus != SimTransferStatus.CANCELLED;
        var canSubmitProof = isActiveTransfer && (
            (isSeller && !hasApprovedSellerProof && !hasPendingSellerProof)
            || (isBuyer && hasApprovedSellerProof && !hasApprovedBuyerProof && !hasPendingBuyerProof)
        );
        var canConfirmTransfer = isActiveTransfer && isBuyer && hasApprovedSellerProof;

        var escrowAmount = trade.Escrow?.EscrowAmount ?? 0m;
        var platformFeeAmount = Math.Round(escrowAmount * DefaultPlatformFeePercent, 2);
        var netSellerPayout = escrowAmount - platformFeeAmount;

        return new SimTransferDetailsDto(
            TradeId: trade.Id,
            AuctionId: trade.AuctionId,
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
            HasPendingSellerProof: hasPendingSellerProof,
            HasPendingBuyerProof: hasPendingBuyerProof,
            SimPhoneNumber: trade.SimNumber?.PhoneNumber ?? "Chưa xác định",
            Proofs: proofDtos,
            EscrowAmount: escrowAmount,
            EscrowStatus: (trade.Escrow?.EscrowStatus ?? EscrowStatus.HELD).ToString(),
            PlatformFeePercent: DefaultPlatformFeePercent,
            PlatformFeeAmount: platformFeeAmount,
            NetSellerPayout: netSellerPayout,
            CompletedAt: trade.TradeTransactionStatus == TradeTransactionStatus.Completed ? trade.CompletedAt : null,
            LiquidationDocumentUrl: trade.TransferContract?.LiquidationDocumentUrl,
            Message: message);
    }
}
