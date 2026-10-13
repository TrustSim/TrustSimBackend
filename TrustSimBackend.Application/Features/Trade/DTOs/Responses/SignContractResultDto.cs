namespace TrustSimBackend.Application.Features.Trade.DTOs.Responses;

/// <summary>
/// DTO trả về kết quả sau khi ký hợp đồng thành công.
/// </summary>
public record SignContractResultDto(
    Guid TradeId,
    string TradeTransactionCode,
    string SignatureRole,
    DateTime SignedAt,
    bool IsFullySigned,
    string ContractStatus,
    string Message
);
