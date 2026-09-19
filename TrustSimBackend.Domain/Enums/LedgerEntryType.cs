namespace TrustSimBackend.Domain.Enums;

/// <summary>
/// Loại dòng bút toán sổ cái kế toán kép: Nợ (DEBIT) hoặc Có (CREDIT).
/// </summary>
public enum LedgerEntryType
{
    /// <summary>
    /// Ghi Nợ (DEBIT).
    /// </summary>
    DEBIT = 1,

    /// <summary>
    /// Ghi Có (CREDIT).
    /// </summary>
    CREDIT = 2
}
