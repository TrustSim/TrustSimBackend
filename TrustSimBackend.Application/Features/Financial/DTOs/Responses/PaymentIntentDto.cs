using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Responses;

/// <summary>
/// DTO thể hiện thông tin của một Payment Intent (Ý định thanh toán).
/// </summary>
public class PaymentIntentDto
{
    public Guid Id { get; set; }
    public string IntentCode { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public Guid WalletId { get; set; }
    public PaymentIntentType IntentType { get; set; }
    public string IntentTypeName => IntentType.ToString();
    public PaymentIntentStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string IdempotencyKey { get; set; } = string.Empty;
    public string GatewayProvider { get; set; } = string.Empty;
    public string? ExternalTransactionId { get; set; }
    public string? QrCodeUrl { get; set; }
    public string? PaymentDetails { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? SucceededAt { get; set; }
    public string? FailedReason { get; set; }
    public DateTime CreatedAt { get; set; }

    public static PaymentIntentDto FromEntity(PaymentIntent entity) => new()
    {
        Id = entity.Id,
        IntentCode = entity.IntentCode,
        UserId = entity.UserId,
        WalletId = entity.WalletId,
        IntentType = entity.IntentType,
        Status = entity.Status,
        Amount = entity.Amount,
        Currency = entity.Currency,
        IdempotencyKey = entity.IdempotencyKey,
        GatewayProvider = entity.GatewayProvider,
        ExternalTransactionId = entity.ExternalTransactionId,
        QrCodeUrl = entity.QrCodeUrl,
        PaymentDetails = entity.PaymentDetails,
        ExpiresAt = entity.ExpiresAt,
        SucceededAt = entity.SucceededAt,
        FailedReason = entity.FailedReason,
        CreatedAt = entity.CreatedDate
    };
}
