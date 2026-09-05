using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Responses;

public class WalletTransactionDto
{
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }
    public string TransactionCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? Description { get; set; }
    public string? QrCodeUrl { get; set; }
    public string? DestinationBankCode { get; set; }
    public string? DestinationBankName { get; set; }
    public string? DestinationAccountNumberMasked { get; set; }
    public string? DestinationAccountHolderName { get; set; }
    public TransactionStatus TransactionStatus { get; set; }
    public WalletTransactionType WalletTransactionType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ExpiredAt { get; set; }
}
