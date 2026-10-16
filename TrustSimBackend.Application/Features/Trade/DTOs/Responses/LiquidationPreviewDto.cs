namespace TrustSimBackend.Application.Features.Trade.DTOs.Responses;

/// <summary>
/// DTO chứa nội dung xem trước Biên bản nghiệm thu và thanh lý giao dịch điện tử.
/// </summary>
public record LiquidationPreviewDto(
    Guid TradeId,
    string TradeTransactionCode,
    string HtmlContent,
    string? LiquidationDocumentUrl,
    string? LiquidationDocumentHash,
    DateTime? LiquidationSealedAt,
    bool IsCompleted,
    string SimPhoneNumber,
    decimal EscrowAmount,
    string BuyerName,
    string SellerName,
    DateTime? TransferDeadline,
    DateTime? LiquidationDeadline
);
