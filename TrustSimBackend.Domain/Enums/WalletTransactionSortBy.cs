namespace TrustSimBackend.Domain.Enums;

/// <summary>
/// Các tiêu chí sắp xếp cho lịch sử giao dịch ví (WalletTransaction).
/// </summary>
public enum WalletTransactionSortBy
{
    CreatedAt = 1,
    Amount = 2,
    Status = 3,
    TransactionType = 4,
    TransactionCode = 5
}
