namespace TrustSimBackend.Application.Features.Financial.DTOs.Responses;

public class UnifiedWalletTransactionDto
{
    public Guid Id { get; set; }
    public string Source { get; set; } = string.Empty; // "WALLET" | "AUCTION"
    public string TransactionType { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty; // "CREDIT" | "DEBIT" | "LOCK"
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ReferenceCode { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}
