using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Financial.DTOs;

/// <summary>
/// DTO thể hiện thông tin chi tiết một giao dịch nạp / rút tiền.
/// </summary>
public class WalletTransactionDto
{
    public Guid WalletTransactionId { get; set; }
    public Guid WalletId { get; set; }
    public string TransactionCode { get; set; } = string.Empty;
    public string? ExternalTransactionId { get; set; }
    public string? QrCodeUrl { get; set; }
    public string? Description { get; set; }
    public TransactionStatus TransactionStatus { get; set; }
    public WalletTransactionType WalletTransactionType { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public string DestinationBankCode { get; set; } = string.Empty;
    public string DestinationBankName { get; set; } = string.Empty;
    public string DestinationAccountNumberMasked { get; set; } = string.Empty;
    public string DestinationAccountHolderName { get; set; } = string.Empty;
}
