namespace TrustSimBackend.Application.Features.Trade.DTOs.Responses;

/// <summary>
/// DTO chứa thông tin giao dịch có hồ sơ bằng chứng sang tên chờ Admin duyệt.
/// </summary>
public record PendingTransferReviewDto(
    Guid TradeId,
    string TradeTransactionCode,
    string SimPhoneNumber,
    Guid SellerId,
    string SellerName,
    Guid BuyerId,
    string BuyerName,
    decimal EscrowAmount,
    string SimTransferStatus,
    List<TransferProofDto> SellerProofs,
    List<TransferProofDto> BuyerProofs,
    bool HasApprovedSeller,
    bool HasApprovedBuyer,
    DateTime CreatedDate,
    string? LiquidationDocumentUrl,
    Guid AuctionId = default,
    string AuctionCode = "",
    string AuctionType = "",
    string AuctionStatus = "",
    decimal WinningPrice = 0m,
    int PendingSellerProofCount = 0,
    int PendingBuyerProofCount = 0
);
