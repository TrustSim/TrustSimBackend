namespace TrustSimBackend.Application.Features.Trade.DTOs.Responses;

/// <summary>
/// DTO chứa nội dung xem trước hợp đồng (HTML Preview) và tiến độ ký của 2 bên.
/// </summary>
public record ContractPreviewDto(
    Guid TradeId,
    string TradeTransactionCode,
    string HtmlContent,
    bool BuyerSigned,
    DateTime? BuyerSignedAt,
    bool SellerSigned,
    DateTime? SellerSignedAt,
    bool IsFullySigned,
    string ContractStatus,
    string? DocumentPdfUrl,
    DateTime SigningDeadline,
    string? DocumentHash = null,
    string? PadesSignatureSerial = null,
    string? PadesSignatureHash = null,
    DateTime? TsaTimestamp = null,
    string? TsaAuthority = null,
    bool IsAutoEnforced = false,
    DateTime? SealedAt = null,
    string? BuyerIp = null,
    string? BuyerAuthMethod = null,
    string? SellerIp = null,
    string? SellerAuthMethod = null,
    string? SimPhoneNumber = null,
    decimal? EscrowAmount = null,
    string? BuyerName = null,
    string? SellerName = null,
    string? WinningNoticeUrl = null,
    string? WinningNoticeHash = null,
    string? LiquidationDocumentUrl = null,
    string? LiquidationDocumentHash = null,
    DateTime? LiquidationSealedAt = null,
    DateTime? TransferDeadline = null,
    DateTime? LiquidationDeadline = null
);

