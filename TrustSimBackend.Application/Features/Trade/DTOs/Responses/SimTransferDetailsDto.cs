namespace TrustSimBackend.Application.Features.Trade.DTOs.Responses;


/// <summary>
/// DTO chi tiết tiến trình bàn giao SIM, sang tên và giải ngân tiền Escrow.
/// </summary>
public record SimTransferDetailsDto(
    Guid TradeId,
    string TradeTransactionCode,
    Guid? SimTransferId,
    string SimTransferStatus,
    DateTime? StartAt,
    DateTime? Deadline,
    string? CancelledReason,
    bool IsSeller,
    bool IsBuyer,
    bool CanSubmitProof,
    bool CanConfirmTransfer,
    string SimPhoneNumber,
    IReadOnlyList<TransferProofDto> Proofs,
    decimal EscrowAmount,
    string EscrowStatus,
    decimal PlatformFeePercent,
    decimal PlatformFeeAmount,
    decimal NetSellerPayout,
    DateTime? CompletedAt,
    string? LiquidationDocumentUrl = null,
    string? Message = null
);
