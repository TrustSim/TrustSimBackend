namespace TrustSimBackend.Application.Features.Trade.DTOs.Responses;

/// <summary>
/// DTO gom nhóm các hồ sơ bằng chứng chuyển nhượng SIM cần duyệt theo Phiên đấu giá (Auction).
/// </summary>
public record AuctionPendingTransferReviewGroupDto(
    Guid AuctionId,
    string AuctionCode,
    string AuctionType,
    string AuctionStatus,
    string SimPhoneNumber,
    decimal WinningPrice,
    DateTime? AuctionStartAt,
    DateTime? AuctionEndAt,
    int TotalTradesCount,
    int TotalPendingProofsCount,
    int PendingSellerProofCount,
    int PendingBuyerProofCount,
    bool HasApprovedSeller,
    bool HasApprovedBuyer,
    List<TransferProofDto> SellerProofs,
    List<TransferProofDto> BuyerProofs,
    List<PendingTransferReviewDto> Trades
);
